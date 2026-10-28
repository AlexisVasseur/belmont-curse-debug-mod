using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using MotherBase.EntityFramework;
using UnityEngine;

namespace BelmontPractice;

/// <summary>
/// Chargement rapide : le personnage relit la sauvegarde injectée, mais certains composants ajoutent ce qu'elle
/// contient sans retirer ce qu'ils avaient déjà (revenir d'une savestate avancée à une savestate de début de partie
/// gardait clés de progression, objets, capacités…). On regénère la sauvegarde du joueur depuis ses composants et on
/// la compare à celle de la savestate : tout élément en trop impose un rechargement complet.
/// </summary>
public static class PlayerSaveCheck
{
    // Listes de progression : un élément présent sur le personnage mais absent de la savestate est un reste.
    static readonly string[] Lists =
    {
        "progressionPersistentSave", "vaniaMoves", "maps", "mapNote", "itemsInInventory", "equippedCharmItems",
        "watchedCutscenes", "completedTutorials", "receivedMainObjectives", "obtainedVirtuesCharacters",
        "obtainedLocationCharacters", "enteredLevels", "unlockedFastTravelLevels", "discoveredLandmarks",
        "usedBiomeTransitions", "arcanaCompletedTasks", "arcanaUnlockedTalents", "arcanaSeenTasks",
    };

    // Dictionnaires sérialisés { m_keys, m_values } : clé en trop ou valeur différente.
    static readonly string[] Dictionaries = { "characterProgressionSteps", "arcanaTaskStates" };

    // Valeurs simples qui doivent être identiques.
    static readonly string[] Values =
    {
        "level", "experienceCurrent", "healthUpgradeFragmentCount", "manaUpgradeFragmentCount", "atkBonusUpgradeCount",
        "defBonusUpgradeCount", "equippedWeaponItem", "equippedSpellItem", "equippedSkin", "currentMainObjective", "currentCurseID",
    };

    /// <summary>
    /// Écarts entre l'état du personnage et la savestate (vide = conforme). Écrase la sauvegarde en mémoire :
    /// l'appelant doit réinjecter celle de la savestate ensuite.
    /// </summary>
    public static List<string> Leftovers(SaveLogic logic, Entity entity, string globalSaveJson)
    {
        var diffs = new List<string>();
        logic.GeneratePlayerSave(entity, SaveOrigin.DebugSpawn);
        var ps = logic._currentSave?.playerSave;
        if (ps == null) { Plugin.Logger.LogWarning("[Rapide] vérification : sauvegarde du joueur non générée"); return diffs; }

        using var actualDoc = JsonDocument.Parse(JsonUtility.ToJson(ps));
        using var targetDoc = JsonDocument.Parse(globalSaveJson);
        var actual = actualDoc.RootElement;
        if (!targetDoc.RootElement.TryGetProperty("playerSave", out var target)) return diffs;

        foreach (var name in Lists)
        {
            var extra = Strings(actual, name).Except(Strings(target, name)).ToList();
            if (extra.Count > 0) diffs.Add($"{name} +{extra.Count} ({Sample(extra)})");
        }
        foreach (var name in Dictionaries)
        {
            var a = Dict(actual, name);
            var t = Dict(target, name);
            var bad = a.Where(kv => !t.TryGetValue(kv.Key, out var v) || v != kv.Value).Select(kv => kv.Key).ToList();
            if (bad.Count > 0) diffs.Add($"{name} {bad.Count} différent(s) ({Sample(bad)})");
        }
        foreach (var name in Values)
        {
            string a = Raw(actual, name), t = Raw(target, name);
            if (a != t) diffs.Add($"{name} {a} au lieu de {t}");
        }
        return diffs;
    }

    static string Sample(List<string> items) => string.Join(", ", items.Take(3)) + (items.Count > 3 ? "…" : "");

    static IEnumerable<string> Strings(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var arr) || arr.ValueKind != JsonValueKind.Array) yield break;
        foreach (var x in arr.EnumerateArray()) yield return x.ValueKind == JsonValueKind.String ? x.GetString() : x.GetRawText();
    }

    static Dictionary<string, string> Dict(JsonElement e, string name)
    {
        var d = new Dictionary<string, string>();
        if (!e.TryGetProperty(name, out var obj) || obj.ValueKind != JsonValueKind.Object) return d;
        if (!obj.TryGetProperty("m_keys", out var keys) || !obj.TryGetProperty("m_values", out var values)) return d;
        var k = keys.EnumerateArray().Select(x => x.ToString()).ToList();
        var v = values.EnumerateArray().Select(x => x.GetRawText()).ToList();
        for (int i = 0; i < k.Count && i < v.Count; i++) d[k[i]] = v[i];
        return d;
    }

    static string Raw(JsonElement e, string name) => e.TryGetProperty(name, out var v) ? v.ToString() : "";
}
