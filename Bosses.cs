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
    public class Boss
    {
        public CharacterID Id;
        public BossData Data;
        public ProgressionKeyID Key;
        public string Name;
        public Sprite Icon;
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
            }
            if (pending.Count == 0 || Time.unscaledTime - startTime > 20f)
            {
                Done = true;
                Plugin.Logger.LogInfo($"[Boss] {All.Count} boss : " + string.Join(", ", All.ConvertAll(b => $"{b.Name} ({b.Id.name}, clé {b.Key?.name ?? "aucune"})")));
            }
        }
        catch (Exception ex)
        {
            Done = true;
            Plugin.Logger.LogWarning("[Boss] liste : " + ex.Message);
        }
    }

    static void Add(CharacterID id, BossData data)
    {
        if (data == null) return;
        var b = new Boss { Id = id, Data = data, Key = data.defeatedProgressionKey };
        try
        {
            var ui = id.LoadCharacterUIDisplayData();
            if (ui != null)
            {
                b.Name = Loc.Get(ui.GetCharacterDisplayName());
                b.Icon = ui.defaultCharacterIcon;
            }
        }
        catch { }
        if (string.IsNullOrEmpty(b.Name)) b.Name = string.IsNullOrEmpty(data.mobDisplayName) ? Pretty(id.name) : data.mobDisplayName;
        b.Icon ??= data.mobIcon;
        All.Add(b);
    }

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

    /// <summary>Vaincu → réapparaît ; pas vaincu → marqué vaincu. Retourne le message à afficher.</summary>
    public static string Toggle(Boss b)
    {
        var p = Progression();
        if (p == null) return "Unavailable (load a game first)";
        if (b.Key == null) return $"{b.Name}: no defeat flag in the game data";
        bool defeated = p.HasProgressionKey(b.Key);
        if (defeated) p.UnsetProgressionKey(b.Key); else p.SetProgressionKey(b.Key);
        Plugin.Logger.LogInfo($"[Boss] {b.Name} : clé {b.Key.name} {(defeated ? "retirée" : "ajoutée")}, étape personnage {Step(b)}");
        return defeated ? $"{b.Name} will be back (re-enter the arena)" : $"{b.Name} marked as defeated";
    }

    /// <summary>Fait réapparaître tous les boss vaincus.</summary>
    public static string RespawnAll()
    {
        var p = Progression();
        if (p == null) return "Unavailable (load a game first)";
        int n = 0;
        foreach (var b in All)
            if (b.Key != null && p.HasProgressionKey(b.Key)) { p.UnsetProgressionKey(b.Key); n++; }
        Plugin.Logger.LogInfo($"[Boss] {n} boss réapparaissent");
        return n == 0 ? "No defeated boss" : $"{n} boss{(n > 1 ? "es" : "")} will be back";
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
