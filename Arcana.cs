using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Il2CppInterop.Runtime;
using MotherBase.EntityFramework;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

namespace BelmontPractice;

/// <summary>Textes du jeu en anglais (quelle que soit la langue choisie), avec repli sur la langue courante.</summary>
public static class Loc
{
    static UnityEngine.Localization.Locale english;
    static bool searched;
    static readonly Dictionary<string, string> cache = new();

    static UnityEngine.Localization.Locale English()
    {
        if (searched) return english;
        searched = true;
        try
        {
            foreach (var l in LocalizationSettings.AvailableLocales.Locales)
                if (l != null && l.Identifier.Code.StartsWith("en", StringComparison.OrdinalIgnoreCase)) { english = l; break; }
        }
        catch (Exception ex) { Plugin.Logger.LogWarning("[Textes] langue anglaise introuvable : " + ex.Message); }
        return english;
    }

    public static string Get(LocalizedString s, string fallback = "")
    {
        if (s == null) return fallback;
        string key = "";
        try { key = s.TableReference.ToString() + "/" + s.TableEntryReference.ToString(); } catch { }
        if (key.Length > 0 && cache.TryGetValue(key, out var hit)) return hit;

        string text = null;
        try
        {
            var en = English();
            if (en != null) text = LocalizationSettings.StringDatabase.GetLocalizedString(s.TableReference, s.TableEntryReference, en);
        }
        catch { }
        if (string.IsNullOrEmpty(text)) { try { text = s.GetLocalizedString(); } catch { } }
        text = Clean(string.IsNullOrEmpty(text) ? fallback : text);
        if (key.Length > 0) cache[key] = text;
        return text;
    }

    /// <summary>Retire les balises propres au jeu (&lt;sprite&gt;, &lt;style&gt;…), que nos textes n'affichent pas.</summary>
    static string Clean(string s) => Regex.Replace(Regex.Replace(s ?? "", "<[^>]+>", ""), "\\s+", " ").Trim();
}

/// <summary>
/// Sorts (arcanes) : un SpellItemData possédé ou non (inventaire), et ses 3 améliorations =
/// les talents de son ArcanaTaskTalentGroup.
/// </summary>
public static class Arcana
{
    public class Talent
    {
        public Spell Owner;
        public ArcanaTalent Data;
        public int Index;
        public string Name => Loc.Get(Data.talentName, $"Upgrade {Index + 1}");
        public string Description => Loc.Get(Data.talentDescription);
    }

    public class Spell
    {
        public ArcanaTaskTalentGroup Group;
        public SpellItemData Item; // variante affichée (celle possédée en priorité)
        // Plusieurs sorts pour une même arcane (ex. Cursed Flame, remplacé plus tard par Absolution)
        public List<SpellItemData> Variants = new();
        public SpellItemData NextVariant()
        {
            int i = Variants.IndexOf(Item);
            return Variants.Count > 1 ? Variants[(i + 1) % Variants.Count] : null;
        }
        public Talent[] Talents = Array.Empty<Talent>();
        public ArcanaCodexData Codex;
        Sprite diamond;
        float nextTry;
        int failures;

        /// <summary>Icône en losange du menu Arcanes du jeu (pas la carte de tarot de l'objet).</summary>
        public Sprite DiamondIcon()
        {
            if (diamond != null || Codex == null || failures >= 5 || Time.unscaledTime < nextTry) return diamond;
            nextTry = Time.unscaledTime + 1f; // le jeu peut ne pas l'avoir encore chargée : on réessaie
            try { diamond = ArcanaUIViewIcon.GetIcon(Codex); }
            catch (Exception ex) { failures++; Plugin.Logger.LogWarning($"[Sorts] icône de {Codex.name} : {ex.Message}"); }
            return diamond;
        }
        public string Name => Item != null ? Loc.Get(Item.itemDisplayName, Item.name) : Group.name;
        public string Description => Item != null ? Loc.Get(Item.itemMainDescription) : "";
    }

    public static readonly List<Spell> Spells = new();
    static float nextResolve;

    public static void Resolve()
    {
        if (Spells.Count > 0 || Time.unscaledTime < nextResolve || !ItemLibrary.Done) return; // attend que tous les sorts soient chargés
        nextResolve = Time.unscaledTime + 2f;
        try
        {
            var items = new List<SpellItemData>();
            foreach (var o in Resources.FindObjectsOfTypeAll(Il2CppType.Of<SpellItemData>()))
            {
                var it = o.TryCast<SpellItemData>();
                if (it != null && !it.abstractItem && !it.isDeprecated) items.Add(it);
            }
            var seen = new HashSet<string>();
            var groups = new List<ArcanaTaskTalentGroup>();
            foreach (var o in Resources.FindObjectsOfTypeAll(Il2CppType.Of<ArcanaTaskTalentGroup>()))
            {
                var g = o.TryCast<ArcanaTaskTalentGroup>();
                if (g != null && g.talents != null && g.talents.Length > 0 && seen.Add(g.name)) groups.Add(g);
            }
            groups.Sort((a, b) => a.arcanaNumber.CompareTo(b.arcanaNumber));
            var codexes = new List<ArcanaCodexData>();
            foreach (var o in Resources.FindObjectsOfTypeAll(Il2CppType.Of<ArcanaCodexData>()))
            {
                var cd = o.TryCast<ArcanaCodexData>();
                if (cd != null && cd.arcanaSpell != null) codexes.Add(cd);
            }

            foreach (var g in groups)
            {
                var spell = new Spell { Group = g };
                foreach (var it in items)
                    if (it.arcanaCharacterID != null && g.linkedArcanaID != null && it.arcanaCharacterID.name == g.linkedArcanaID.name
                        && !spell.Variants.Exists(v => v.itemID == it.itemID))
                        spell.Variants.Add(it);
                // Variante de base en premier (celle qui est remplacée)
                spell.Variants.Sort((a, b) => string.IsNullOrEmpty(a.spellIDToReplaceOnAdded).CompareTo(string.IsNullOrEmpty(b.spellIDToReplaceOnAdded)) * -1);
                spell.Item = spell.Variants.Count > 0 ? spell.Variants[0] : null;
                if (spell.Variants.Count > 1)
                    Plugin.Logger.LogInfo($"[Sorts] {g.name} : variantes {string.Join(", ", spell.Variants.ConvertAll(v => v.name))}");
                foreach (var cd in codexes)
                    if (spell.Variants.Exists(v => v.itemID == cd.arcanaSpell.itemID)) { spell.Codex = cd; break; }
                var talents = new List<Talent>();
                for (int i = 0; i < g.talents.Length; i++)
                    if (g.talents[i] != null) talents.Add(new Talent { Owner = spell, Data = g.talents[i], Index = i });
                spell.Talents = talents.ToArray();
                Spells.Add(spell);
            }
            // Comme dans le jeu : l'arcane à deux variantes (Cursed Flame / Absolution) en premier, les autres dans l'ordre
            int dual = Spells.FindIndex(s => s.Variants.Count > 1);
            if (dual > 0) { var d = Spells[dual]; Spells.RemoveAt(dual); Spells.Insert(0, d); }
            if (Spells.Count > 0)
                Plugin.Logger.LogInfo($"[Sorts] {Spells.Count} arcanes trouvées ({items.Count} sorts en mémoire)");
        }
        catch (Exception ex) { Plugin.Logger.LogWarning("[Sorts] " + ex.Message); Spells.Clear(); }
    }

    static bool Player(out Entity e)
    {
        e = null;
        return MainCharacterSingleton.TryGetEntity(out e) && e != null;
    }

    static Inventory Inventory() =>
        Player(out var e) ? e.GetComponent<InventoryComponent>()?.inventory : null;

    static ArcanaTaskTalentComponent Talents() =>
        Player(out var e) ? e.GetComponent<ArcanaTaskTalentComponent>() : null;

    public static bool? IsOwned(Spell s)
    {
        if (s.Item == null) return null;
        try
        {
            var inv = Inventory();
            if (inv == null) return null;
            foreach (var it in inv._itemsInInventory)
            {
                if (it == null) continue;
                // Une variante possédée devient la variante affichée
                foreach (var v in s.Variants)
                    if (it.itemID == v.itemID) { s.Item = v; return true; }
            }
            return false;
        }
        catch { return null; }
    }

    public static bool IsEquipped(Spell s)
    {
        try { var eq = Inventory()?._equippedSpell; return eq != null && s.Item != null && eq.itemID == s.Item.itemID; }
        catch { return false; }
    }

    public static bool? IsUnlocked(Talent t)
    {
        try { var c = Talents(); return c == null ? null : c.IsTalentUnlocked(t.Data); }
        catch { return null; }
    }

    public static string ToggleSpell(Spell s)
    {
        var owned = IsOwned(s);
        var inv = Inventory();
        if (owned == null || inv == null) return "Spell unavailable (load a game first)";
        try
        {
            if (owned.Value)
            {
                // Un sort retiré ne doit pas rester équipé
                if (IsEquipped(s)) inv.UnequipSpell();
                inv.RemoveItemFromInventory(s.Item.itemID);
            }
            else AddSpell(inv, s);
            bool now = IsOwned(s) == true;
            Plugin.Logger.LogInfo($"[Sorts] {s.Item.name} → {(now ? "ajouté" : "retiré")}");
            return $"{s.Name}: {(now ? "unlocked" : "removed")}";
        }
        catch (Exception ex) { return "Toggle failed: " + ex.Message; }
    }

    /// <summary>
    /// Ajoute le sort affiché à l'inventaire. Certains sorts (ex. Absolution) sont refusés par l'ajout normal :
    /// on essaie alors l'ajout « d'initialisation » du jeu, puis l'ajout direct dans la liste.
    /// </summary>
    static void AddSpell(Inventory inv, Spell s)
    {
        var item = s.Item;
        inv.AddItemToInventory(item, false);
        if (IsOwned(s) == true) return;
        Plugin.Logger.LogInfo($"[Sorts] {item.name} refusé par AddItemToInventory, essai FromInit");

        s.Item = item;
        inv.AddItemToInventoryFromInit(item, false);
        if (IsOwned(s) == true) return;
        Plugin.Logger.LogInfo($"[Sorts] {item.name} refusé par AddItemToInventoryFromInit, ajout direct");

        s.Item = item;
        inv._itemsInInventory.Add(item);
        if (Player(out var e))
        {
            try { item.OnAddedToInventory(e, inv, false); }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[Sorts] OnAddedToInventory({item.name}) : {ex.Message}"); }
        }
    }

    /// <summary>Passe à la variante suivante (ex. Cursed Flame ↔ Absolution), en remplaçant celle possédée.</summary>
    public static string SwitchVariant(Spell s)
    {
        var next = s.NextVariant();
        if (next == null) return null;
        var owned = IsOwned(s);
        var inv = Inventory();
        try
        {
            var current = s.Item;
            if (owned == true && inv != null)
            {
                bool wasEquipped = IsEquipped(s);
                if (wasEquipped) inv.UnequipSpell();
                // Les deux variantes ne doivent jamais être possédées en même temps
                foreach (var v in s.Variants)
                    if (v.itemID != next.itemID && InInventory(inv, v)) inv.RemoveItemFromInventory(v.itemID);
                s.Item = next;
                AddSpell(inv, s);
                if (wasEquipped) inv.EquipSpellFromInventory(next);
            }
            s.Item = next;
            Plugin.Logger.LogInfo($"[Sorts] variante {current.name} → {next.name}");
            return $"{s.Name}: switched";
        }
        catch (Exception ex) { return "Switch failed: " + ex.Message; }
    }

    static bool InInventory(Inventory inv, SpellItemData v)
    {
        foreach (var it in inv._itemsInInventory) if (it != null && it.itemID == v.itemID) return true;
        return false;
    }

    /// <summary>Clic droit : équipe l'arcane (débloquée au besoin) ou la retire de l'emplacement.</summary>
    public static string ToggleEquip(Spell s)
    {
        var inv = Inventory();
        if (inv == null || s.Item == null) return "Spell unavailable (load a game first)";
        try
        {
            if (IsEquipped(s)) { inv.UnequipSpell(); return $"{s.Name}: unequipped"; }
            if (IsOwned(s) != true) AddSpell(inv, s);
            if (IsOwned(s) != true) return $"{s.Name}: could not be unlocked";
            inv.EquipSpellFromInventory(s.Item);
            return IsEquipped(s) ? $"{s.Name}: equipped" : $"{s.Name}: could not be equipped";
        }
        catch (Exception ex) { return "Equip failed: " + ex.Message; }
    }

    public static string ToggleTalent(Talent t)
    {
        var c = Talents();
        if (c == null || !Player(out var e)) return "Upgrade unavailable (load a game first)";
        try
        {
            if (c.IsTalentUnlocked(t.Data)) c.LockTalent(e, t.Data);
            else if (!c.TryUnlockTalent(e, t.Owner.Group, t.Index))
            {
                // Pas assez de points : on le débloque quand même
                if (!c._unlockedTalents.Contains(t.Data)) c._unlockedTalents.Add(t.Data);
                c.RefreshActiveTasksAndTalents(e);
            }
            bool now = c.IsTalentUnlocked(t.Data);
            Plugin.Logger.LogInfo($"[Sorts] talent {t.Data.name} → {(now ? "débloqué" : "verrouillé")}");
            return $"{t.Owner.Name} · upgrade {t.Index + 1}: {(now ? "unlocked" : "locked")}";
        }
        catch (Exception ex) { return "Toggle failed: " + ex.Message; }
    }
}
