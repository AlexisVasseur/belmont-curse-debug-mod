using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Il2CppInterop.Runtime;
using MotherBase.EntityFramework;
using UnityEngine;

namespace BelmontPractice;

/// <summary>
/// Armes (Armurerie) : des familles (lignes) de 4 paliers (colonnes). Une arme est possédée si elle est
/// dans l'inventaire. Les lignes du menu du jeu viennent de son prefab : ici on regroupe par famille
/// d'après le nom de l'arme, et on trie par palier (WeaponTier1..4).
/// </summary>
public static class Weapons
{
    public class Weapon
    {
        public WeaponItemData Item;
        public int Tier;
        public string Name => Loc.Get(Item.itemDisplayName, Item.name);
        public string Description => Loc.Get(Item.itemMainDescription);
        public string Special => Loc.Get(Item.weaponSpecialDescription);
    }

    public class Family
    {
        public string Key;
        public List<Weapon> Weapons = new();
    }

    public static readonly List<Family> Families = new();
    static float nextResolve;

    /// <summary>Clé de famille : le nom de l'arme sans palier ni suffixe (ex. « Sword_T2_ItemData » → « Sword »).</summary>
    static string FamilyKey(WeaponItemData w)
    {
        // Les paliers supérieurs pointent souvent vers l'arme de base
        var root = w;
        for (int guard = 0; guard < 6 && root.parentWeapon != null; guard++) root = root.parentWeapon;
        string n = root.name;
        n = Regex.Replace(n, "(_?ItemData|_?WeaponData|_?Data)$", "", RegexOptions.IgnoreCase);
        n = Regex.Replace(n, "_?(T|Tier|Lvl|Level)?\\d+$", "", RegexOptions.IgnoreCase);
        int us = n.IndexOf('_');
        return us > 0 ? n.Substring(0, us) : n;
    }

    // Ordre des lignes de l'Armurerie du jeu
    static readonly string[] RowOrder = { "WeaponOneHanded", "WeaponAndShield", "WeaponSpear", "WeaponFist", "WeaponTwoHanded", "WeaponDualWield", "WeaponWhip" };
    static int OrderOf(string key) { int i = Array.IndexOf(RowOrder, key); return i < 0 ? 100 : i; }

    static int TierOf(WeaponItemData w)
    {
        try
        {
            var m = Regex.Match(w.weaponTier != null ? w.weaponTier.name : "", "(\\d+)");
            if (m.Success) return int.Parse(m.Groups[1].Value);
        }
        catch { }
        return 0;
    }

    public static void Resolve()
    {
        if (Families.Count > 0 || Time.unscaledTime < nextResolve || !ItemLibrary.Done) return; // attend que toutes les armes soient chargées
        nextResolve = Time.unscaledTime + 2f;
        try
        {
            var seen = new HashSet<string>();
            var byKey = new Dictionary<string, Family>();
            var log = new StringBuilder();
            foreach (var o in Resources.FindObjectsOfTypeAll(Il2CppType.Of<WeaponItemData>()))
            {
                var w = o.TryCast<WeaponItemData>();
                if (w == null || w.abstractItem || w.isDeprecated || string.IsNullOrEmpty(w.itemID) || !seen.Add(w.itemID)) continue;
                // Copies utilisées par le boss Doppelganger : pas des armes du joueur
                if (w.name.IndexOf("Doppelganger", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                string key = FamilyKey(w);
                if (!byKey.TryGetValue(key, out var fam)) byKey[key] = fam = new Family { Key = key };
                fam.Weapons.Add(new Weapon { Item = w, Tier = TierOf(w) });
                log.Append($"\n  - {w.name} : famille={key} palier={TierOf(w)} type={w.GetIl2CppType().Name} parent={(w.parentWeapon != null ? w.parentWeapon.name : "-")} variance={w.weaponVarianceType}");
            }
            foreach (var f in byKey.Values)
            {
                f.Weapons.Sort((a, b) => a.Tier != b.Tier ? a.Tier.CompareTo(b.Tier) : string.CompareOrdinal(a.Item.name, b.Item.name));
                Families.Add(f);
            }
            Families.Sort((a, b) => OrderOf(a.Key).CompareTo(OrderOf(b.Key)));
            if (Families.Count > 0)
                Plugin.Logger.LogInfo($"[Armes] {seen.Count} armes en {Families.Count} familles :{log}");
        }
        catch (Exception ex) { Plugin.Logger.LogWarning("[Armes] " + ex.Message); Families.Clear(); }
    }

    static Inventory Inventory() =>
        MainCharacterSingleton.TryGetEntity(out Entity e) && e != null ? e.GetComponent<InventoryComponent>()?.inventory : null;

    public static bool? IsOwned(Weapon w)
    {
        try
        {
            var inv = Inventory();
            if (inv == null) return null;
            foreach (var it in inv._itemsInInventory)
                if (it != null && it.itemID == w.Item.itemID) return true;
            return false;
        }
        catch { return null; }
    }

    public static bool IsEquipped(Weapon w)
    {
        try { var eq = Inventory()?._equippedWeapon; return eq != null && eq.itemID == w.Item.itemID; }
        catch { return false; }
    }

    public static string Toggle(Weapon w)
    {
        var inv = Inventory();
        var owned = IsOwned(w);
        if (inv == null || owned == null) return "Weapon unavailable (load a game first)";
        try
        {
            if (owned.Value)
            {
                // Rose doit toujours avoir une arme en main
                if (IsEquipped(w)) return "Can't remove the equipped weapon: equip another one first (right-click)";
                inv.RemoveItemFromInventory(w.Item.itemID);
            }
            else
            {
                inv.AddItemToInventory(w.Item, false);
                if (IsOwned(w) != true) inv.AddItemToInventoryFromInit(w.Item, false);
            }
            bool now = IsOwned(w) == true;
            Plugin.Logger.LogInfo($"[Armes] {w.Item.name} → {(now ? "ajoutée" : "retirée")}");
            return $"{w.Name}: {(now ? "unlocked" : "removed")}";
        }
        catch (Exception ex) { return "Toggle failed: " + ex.Message; }
    }

    public static string Equip(Weapon w)
    {
        var inv = Inventory();
        if (inv == null) return "Weapon unavailable (load a game first)";
        try
        {
            if (IsOwned(w) != true) inv.AddItemToInventory(w.Item, false);
            inv.EquipWeaponFromInventory(w.Item);
            Plugin.Logger.LogInfo($"[Armes] {w.Item.name} équipée");
            return $"{w.Name}: equipped";
        }
        catch (Exception ex) { return "Equip failed: " + ex.Message; }
    }
}
