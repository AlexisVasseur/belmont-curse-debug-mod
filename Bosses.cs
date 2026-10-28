using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using MotherBase.EntityFramework;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace BelmontPractice;

/// <summary>
/// Boss du jeu (CharacterManager.bossDataDictionary). Un boss est vaincu quand la clé de progression de son
/// BossData (defeatedProgressionKey) est dans le ProgressionComponent du joueur : la retirer le fait réapparaître
/// (le déclencheur de l'arène relance le combat à la prochaine entrée dans la salle).
/// </summary>
public static class Bosses
{
    /// <summary>
    /// Ce qu'on garde d'un boss. Pas son BossData : le garder chargé empêchait le jeu de le recharger à neuf
    /// (après un premier combat, le boss n'était plus créé). On copie ce qui sert puis on le relâche.
    /// </summary>
    public class Boss
    {
        public CharacterID Id;
        public string DataName, KeyGuid, KeyName;
        public readonly HashSet<string> GateDataNames = new();
        public bool IgnoreAbsorption;
        public string Name;
        public Sprite Icon;

        ProgressionKeyID keyCache;
        float nextKeySearch;

        /// <summary>Clé « vaincu », retrouvée parmi les clés chargées (null si introuvable pour l'instant).</summary>
        public ProgressionKeyID Key
        {
            get
            {
                if (KeyGuid == null) return null;
                if (keyCache != null) return keyCache; // comparaison Unity : null si l'objet a été détruit
                if (Time.unscaledTime < nextKeySearch) return null;
                nextKeySearch = Time.unscaledTime + 2f;
                foreach (var o in Resources.FindObjectsOfTypeAll(Il2CppInterop.Runtime.Il2CppType.Of<ProgressionKeyID>()))
                {
                    var k = o.TryCast<ProgressionKeyID>();
                    if (k != null && k.guid == KeyGuid) { keyCache = k; break; }
                }
                return keyCache;
            }
        }
    }

    public static readonly List<Boss> All = new();
    static readonly List<(CharacterID id, AsyncOperationHandle<BossData> h)> pending = new();
    static bool started;
    static float startTime;
    public static bool Done { get; private set; }

    /// <summary>Chargement asynchrone de la liste (nos propres requêtes, jamais les AssetReference du jeu).</summary>
    public static void Resolve()
    {
        if (Done) return;
        try
        {
            if (!started)
            {
                var elements = CharacterManager.Get()?.bossDataDictionary?.elements;
                if (elements == null) return; // pas encore en jeu
                started = true;
                startTime = Time.unscaledTime;
                for (int i = 0; i < elements.Count; i++)
                {
                    var e = elements[i];
                    if (e == null || e.id == null || e.item == null) continue;
                    var asset = e.item.Asset;
                    if (asset != null) { Add(e.id, asset.TryCast<BossData>()); continue; }
                    if (!e.item.RuntimeKeyIsValid()) continue;
                    pending.Add((e.id, Addressables.LoadAssetAsync<BossData>(e.item.RuntimeKey)));
                }
            }

            for (int i = pending.Count - 1; i >= 0; i--)
            {
                var (id, h) = pending[i];
                if (!h.IsDone) continue;
                if (h.Status == AsyncOperationStatus.Succeeded) Add(id, h.Result);
                pending.RemoveAt(i);
                try { Addressables.Release(h); } catch { } // relâché aussitôt : le jeu doit pouvoir le décharger / recharger
            }
            if (pending.Count == 0 || Time.unscaledTime - startTime > 20f)
            {
                Done = true;
                Plugin.Logger.LogInfo($"[Boss] {All.Count} boss : " + string.Join(", ", All.ConvertAll(b => $"{b.Name} ({b.Id.name}, clé {b.KeyName ?? "aucune"})")));
            }
        }
        catch (Exception ex)
        {
            Done = true;
            Plugin.Logger.LogWarning("[Boss] liste : " + ex.Message);
        }
    }

    /// <summary>Déclarés boss par le jeu mais sans combat (Brauner : une cinématique) : absents de l'onglet.</summary>
    static readonly HashSet<string> NotFights = new() { "CharacterID_Brauner" };

    static void Add(CharacterID id, BossData data)
    {
        if (data == null || NotFights.Contains(id?.name ?? "")) return;
        var key = data.defeatedProgressionKey;
        var b = new Boss
        {
            Id = id, DataName = data.name, KeyGuid = key?.guid, KeyName = key?.name,
            IgnoreAbsorption = data.ignoreAbsorptionForSpawnCondition,
        };
        b.GateDataNames.Add(data.name);
        if (data.bossDataOverrideForBossGates != null) b.GateDataNames.Add(data.bossDataOverrideForBossGates.name);
        KnownNames.TryGetValue(id.name ?? "", out b.Name);
        try
        {
            var ui = id.LoadCharacterUIDisplayData();
            if (ui != null)
            {
                // Nom du jeu en repli (boss ajouté par une mise à jour) : le nom par défaut, pas celui qui dépend de
                // la progression (« ??? » ou « The Fallen » tant que le personnage n'est pas rencontré)
                if (string.IsNullOrEmpty(b.Name)) b.Name = Known(Loc.Get(ui.GetDefaultCharacterDisplayName()));
                b.Icon = ui.defaultCharacterIcon;
            }
        }
        catch { }
        if (string.IsNullOrEmpty(b.Name)) b.Name = Known(data.mobDisplayName) ?? Pretty(id.name);
        All.Add(b);
    }

    /// <summary>
    /// Noms des boss, gardés dans le mod : le jeu n'affiche le vrai nom qu'une fois le personnage rencontré
    /// (nouvelle partie : « ??? », Leon en « The Fallen »).
    /// </summary>
    static readonly Dictionary<string, string> KnownNames = new()
    {
        ["CharacterID_Leon"] = "Leon",
        ["CharacterID_Joan"] = "Joan",
        ["CharacterID_Medusa"] = "Medusa",
        ["CharacterID_Death"] = "Death",
        ["CharacterID_Isaac"] = "Isaac",
        ["CharacterID_Carmilla"] = "Carmilla",
        ["CharacterID_Talos"] = "Talos",
        ["CharacterID_Succubus"] = "Succubus",
        ["CharacterID_StLouis"] = "Saint Louis",
        ["CharacterID_Mammon"] = "Mammon",
        ["CharacterID_Arthur"] = "Arthur",
        ["CharacterID_Doppelganger"] = "Doppelganger",
        ["CharacterID_Dracula"] = "Dracula",
        ["CharacterID_DraculaBeast"] = "Dracula (Beast)",
    };

    static string Known(string name) => string.IsNullOrWhiteSpace(name) || name.Trim('?', ' ').Length == 0 ? null : name;

    static string Pretty(string s) =>
        Regex.Replace(Regex.Replace(s ?? "", "^(CharacterID|CID)_?", ""), "(?<=[a-z])(?=[A-Z])", " ").Replace('_', ' ').Trim();

    static ProgressionComponent Progression() =>
        MainCharacterSingleton.TryGetEntity(out Entity e) && e != null ? e.GetComponent<ProgressionComponent>() : null;

    /// <summary>true = vaincu, false = pas vaincu, null = inconnu (pas en jeu, boss sans clé).</summary>
    public static bool? IsDefeated(Boss b)
    {
        try
        {
            var p = Progression();
            if (p == null || b.Key == null) return null;
            return p.HasProgressionKey(b.Key);
        }
        catch { return null; }
    }

    /// <summary>
    /// Met le boss dans l'état demandé, en entier (clé « vaincu » et absorption), quel que soit l'état actuel.
    /// Retourne le message à afficher.
    /// </summary>
    public static string SetDefeated(Boss b, bool defeated)
    {
        var p = Progression();
        if (p == null) return "Unavailable (load a game first)";
        if (b.Key == null) return b.KeyGuid == null ? $"{b.Name}: no defeat flag in the game data" : $"{b.Name}: defeat flag not loaded yet, try again";
        string before = $"clé={p.HasProgressionKey(b.Key)} étape={Step(b)}";
        if (defeated) { p.SetProgressionKey(b.Key); SetStep(b, CharacterProgressionStep.Absorbed); BossRespawn.Pending.Remove(b.Id.name); }
        else { Respawn(p, b); BossRespawn.Pending.Add(b.Id.name); }
        Plugin.Logger.LogInfo($"[Boss] {b.Name} → {(defeated ? "Defeated" : "Alive")} : avant {before}, après clé={p.HasProgressionKey(b.Key)} étape={Step(b)}");
        LogTriggers("après clic");
        return defeated ? $"{b.Name}: defeated" : $"{b.Name}: alive — now use TP, then walk into its arena";
    }

    /// <summary>
    /// Retire la clé « vaincu » et l'absorption du boss (après le combat, le joueur absorbe son arcane :
    /// un boss absorbé ne réapparaît pas non plus). Le combat la redonne à la fin.
    /// </summary>
    static void Respawn(ProgressionComponent p, Boss b)
    {
        p.UnsetProgressionKey(b.Key);
        ResetGates(b);
        if (b.IgnoreAbsorption) return;
        SetStep(b, CharacterProgressionStep.Found, onlyIfAbsorbed: true);
    }

    /// <summary>
    /// Efface l'état mémorisé des portes de l'arène (« ouvertes pour toujours » après la victoire) :
    /// elles se referment au début du combat et se rouvrent à la fin, comme la première fois.
    /// </summary>
    static void ResetGates(Boss b)
    {
        try
        {
            var dict = CurrentBossGateState._bossGateState;
            if (dict == null) return;
            var names = b.GateDataNames;
            var toRemove = new List<BossData>();
            foreach (var k in dict.Keys) if (k != null && names.Contains(k.name)) toRemove.Add(k);
            foreach (var k in toRemove) dict.Remove(k);
            Plugin.Logger.LogInfo($"[Boss] {b.Name} : état des portes d'arène effacé ({toRemove.Count})");
        }
        catch (Exception ex) { Plugin.Logger.LogWarning($"[Boss] portes {b.Name} : {ex.Message}"); }
    }

    static void SetStep(Boss b, CharacterProgressionStep step, bool onlyIfAbsorbed = false)
    {
        try
        {
            if (!MainCharacterSingleton.TryGetEntity(out Entity e) || e == null) return;
            var c = e.GetComponent<CharacterProgressionComponent>();
            if (c == null) return;
            if (onlyIfAbsorbed && c.GetProgressionStep(b.Id) != CharacterProgressionStep.Absorbed) return;
            c.SetProgressionStep(b.Id, step, true, true);
        }
        catch (Exception ex) { Plugin.Logger.LogWarning($"[Boss] étape {b.Name} : {ex.Message}"); }
    }

    /// <summary>
    /// Bouton Fight : remet le boss en vie et téléporte devant son arène (niveau rechargé, combat remis à zéro),
    /// soit Alive puis TP en un clic.
    /// </summary>
    public static string Fight(Boss b, string encounterKey)
    {
        if (Arenas.Get(encounterKey) == null) return $"{b.Name}: enter its arena once so the mod can remember the door";
        string msg = SetDefeated(b, false);
        if (IsDefeated(b) != false) return msg; // échec (pas en jeu, clé introuvable…)
        return Arenas.RequestTeleport(b, encounterKey);
    }

    /// <summary>Fait réapparaître tous les boss vaincus.</summary>
    public static string RespawnAll()
    {
        var p = Progression();
        if (p == null) return "Unavailable (load a game first)";
        int n = 0;
        foreach (var b in All)
            if (b.Key != null && p.HasProgressionKey(b.Key)) { Respawn(p, b); BossRespawn.Pending.Add(b.Id.name); n++; }
        Plugin.Logger.LogInfo($"[Boss] {n} boss réapparaissent");
        return n == 0 ? "No defeated boss" : $"{n} boss{(n > 1 ? "es" : "")} will be back";
    }

    /// <summary>Diagnostic (option Debug/RelevesBoss) : déclencheurs d'arène chargés et scénarios de combat.</summary>
    public static void LogTriggers(string when)
    {
        if (!Plugin.BossDiagnostics.Value) return;
        try
        {
            var found = UnityEngine.Resources.FindObjectsOfTypeAll(Il2CppInterop.Runtime.Il2CppType.Of<BossFightTrigger>());
            var parts = new List<string>();
            foreach (var o in found)
            {
                var t = o.TryCast<BossFightTrigger>();
                if (t == null || !t.gameObject.scene.IsValid()) continue; // prefabs hors scène
                string defeated;
                try { defeated = t.IsBossDefeated().ToString(); } catch (Exception ex) { defeated = "? " + ex.Message; }
                parts.Add($"'{t.name}' boss={t.bossData?.name ?? "?"} actif={t.gameObject.activeInHierarchy} vaincu={defeated} déclenché={t.encounterHasBeenTriggered}");
            }
            Plugin.Logger.LogInfo($"[Boss] déclencheurs ({when}) : {(parts.Count == 0 ? "aucun" : string.Join(" | ", parts))}");
            BossDiag.DumpDirectors(when);
        }
        catch (Exception ex) { Plugin.Logger.LogWarning("[Boss] déclencheurs : " + ex.Message); }
    }

    /// <summary>Étape de progression du personnage (diagnostic : « absorbé » peut aussi empêcher le combat).</summary>
    static string Step(Boss b)
    {
        try
        {
            if (!MainCharacterSingleton.TryGetEntity(out Entity e) || e == null) return "?";
            var c = e.GetComponent<CharacterProgressionComponent>();
            return c == null ? "?" : c.GetProgressionStep(b.Id).ToString();
        }
        catch (Exception ex) { return "? " + ex.Message; }
    }
}
