using System;
using System.Collections.Generic;
using System.Linq;
using Il2CppInterop.Runtime;
using MotherBase.EntityFramework;
using UnityEngine;

namespace BelmontPractice;

/// <summary>
/// Fait vraiment revenir un boss dont le combat est piloté par un scénario (CutsceneDirector) :
/// le scénario de l'arène vérifie des conditions (clés de progression, arcanes absorbées, cinématiques vues)
/// avant d'arriver au lancement du combat (SetupBoss). À l'entrée dans l'arène d'un boss remis « Alive »,
/// on cherche un chemin du départ jusqu'au combat, on applique le minimum de changements pour qu'il soit suivi,
/// puis on relance le scénario. Si le joueur ressort sans avoir battu le boss, tout est remis comme avant.
/// </summary>
public static class BossRespawn
{
    /// <summary>Boss remis « Alive » par l'onglet Bosses (CharacterID), dont le combat doit être forcé.</summary>
    public static readonly HashSet<string> Pending = new();

    enum Kind { Key, Arcana, Cutscene }

    class Cond
    {
        public Kind kind;
        public ProgressionKeyID key;
        public CharacterID character;
        public CharacterProgressionStep step;
        public string cutsceneId;
        public string Id => kind switch { Kind.Key => "clé " + key?.name, Kind.Arcana => $"arcane {character?.name}≥{step}", _ => "cinématique " + cutsceneId };
    }

    class Change { public Cond cond; public bool original; public CharacterProgressionStep originalStep; }

    static Bosses.Boss activeBoss;
    static float searchUntil;
    static readonly List<Change> applied = new();

    // ---------- Cycle de vie ----------

    public static void OnEnterArena(Bosses.Boss boss)
    {
        if (boss?.Id == null || !Pending.Contains(boss.Id.name)) return;
        if (Bosses.IsDefeated(boss) == true) { Pending.Remove(boss.Id.name); return; }
        activeBoss = boss;
        searchUntil = Time.unscaledTime + 6f; // le scénario n'existe qu'une fois la salle chargée
    }

    public static void OnLeaveArena()
    {
        searchUntil = 0f;
        if (activeBoss == null) return;
        bool beaten = Bosses.IsDefeated(activeBoss) == true;
        if (beaten) Pending.Remove(activeBoss.Id.name);
        Revert(); // rien à faire si déjà remis à la victoire
        applied.Clear();
        activeBoss = null;
    }

    public static void Update()
    {
        try { UpdateTrace(); } catch (Exception ex) { traced = null; Plugin.Logger.LogWarning("[Respawn] relevé : " + ex.Message); }
        // Boss battu : la progression modifiée pour forcer le combat (porte, clés…) est remise aussitôt,
        // avant que le scénario ne reparte de son début (il retrouve ainsi l'état d'avant le combat).
        if (activeBoss != null && applied.Count > 0 && Bosses.IsDefeated(activeBoss) == true)
        {
            Plugin.Logger.LogInfo($"[Respawn] {activeBoss.Name} battu : progression d'avant le combat remise");
            Revert();
            applied.Clear();
            Pending.Remove(activeBoss.Id.name);
        }
        if (searchUntil <= 0f || activeBoss == null) return;
        if (Time.unscaledTime > searchUntil)
        {
            searchUntil = 0f;
            Plugin.Logger.LogInfo($"[Respawn] {activeBoss.Name} : aucun scénario de combat trouvé dans l'arène");
            return;
        }
        var director = FindDirector(activeBoss);
        if (director == null) return;
        searchUntil = 0f;
        try { Force(director, activeBoss); }
        catch (Exception ex) { Plugin.Logger.LogWarning($"[Respawn] {activeBoss.Name} : {ex}"); }
    }

    // ---------- Scénario de l'arène ----------

    static bool IsBossNode(NodeDataBase n)
    {
        string t = n?.GetIl2CppType().Name ?? "";
        return t == "ResolverSetupBossNodeData" || t == "ResolverStartBossFightNodeData";
    }

    static CutsceneDirector FindDirector(Bosses.Boss boss)
    {
        string shortName = boss.Id.name.Replace("CharacterID_", "");
        CutsceneDirector any = null;
        int count = 0;
        foreach (var o in Resources.FindObjectsOfTypeAll(Il2CppType.Of<CutsceneDirector>()))
        {
            var d = o.TryCast<CutsceneDirector>();
            if (d == null || !d.gameObject.scene.IsValid() || !d.isActiveAndEnabled) continue;
            var nodes = d._resolverData?.Nodes;
            if (nodes == null) continue;
            foreach (var n in nodes)
            {
                if (!IsBossNode(n)) continue;
                count++;
                any = d;
                // Le nœud de mise en place cite l'action du boss (« LEON_Real_LeonIntro_Setup »)
                var setup = n.TryCast<ResolverSetupBossNodeData>();
                string action = setup?.ActionData?.Value?.name ?? "";
                if (action.IndexOf(shortName, StringComparison.OrdinalIgnoreCase) >= 0) return d;
                break;
            }
        }
        return count == 1 ? any : null;
    }

    static Cond ReadCond(NodeDataBase n)
    {
        var pk = n.TryCast<ResolverConditionProgressionKeyNodeData>();
        if (pk != null) return pk.ProgressionKey == null || pk.ProgressionKey.UseRef || pk.ProgressionKey.Value == null ? null
            : new Cond { kind = Kind.Key, key = pk.ProgressionKey.Value };
        var ar = n.TryCast<ResolverConditionArcanaStateNodeData>();
        if (ar != null) return ar.Character == null || ar.Character.UseRef || ar.Character.Value == null ? null
            : new Cond { kind = Kind.Arcana, character = ar.Character.Value, step = ar.ArcanaState };
        var wc = n.TryCast<ResolverConditionHasWatchedCutsceneNodeData>();
        if (wc != null) return wc.CutsceneData == null || wc.CutsceneData.UseRef || wc.CutsceneData.Value == null ? null
            : new Cond { kind = Kind.Cutscene, cutsceneId = wc.CutsceneData.Value.ID };
        return null;
    }

    static bool IsCondition(NodeDataBase n) => n?.TryCast<ResolverConditionNodeDataBase>() != null;

    static Entity Player() => MainCharacterSingleton.TryGetEntity(out Entity e) ? e : null;

    static bool Evaluate(Cond c)
    {
        var e = Player();
        switch (c.kind)
        {
            case Kind.Key: return e?.GetComponent<ProgressionComponent>()?.HasProgressionKey(c.key) == true;
            case Kind.Arcana:
                var cp = e?.GetComponent<CharacterProgressionComponent>();
                return cp != null && cp.GetProgressionStep(c.character) >= c.step;
            default: return CutsceneManager.Instance?.HasWatchedCutscene(c.cutsceneId) == true;
        }
    }

    static CharacterProgressionStep Step(CharacterID c) =>
        Player()?.GetComponent<CharacterProgressionComponent>()?.GetProgressionStep(c) ?? CharacterProgressionStep.Unknown;

    static void Set(Cond c, bool value, CharacterProgressionStep restoreStep = CharacterProgressionStep.Unknown)
    {
        var e = Player();
        switch (c.kind)
        {
            case Kind.Key:
                var p = e?.GetComponent<ProgressionComponent>();
                if (value) p?.SetProgressionKey(c.key); else p?.UnsetProgressionKey(c.key);
                break;
            case Kind.Arcana:
                var cp = e?.GetComponent<CharacterProgressionComponent>();
                var target = restoreStep != CharacterProgressionStep.Unknown ? restoreStep
                    : value ? c.step : (CharacterProgressionStep)Math.Max(0, (int)c.step - 1);
                cp?.SetProgressionStep(c.character, target, true, true);
                break;
            default:
                var m = CutsceneManager.Instance;
                if (m == null) break;
                if (value) m.WatchCutscene(c.cutsceneId);
                else m._watchedCutscenes?.Remove(c.cutsceneId);
                break;
        }
    }

    /// <summary>
    /// Chemin du départ jusqu'au combat : à chaque condition, branche « vraie » (suites) ou « fausse » (sinon).
    /// Retourne les conditions à imposer, en minimisant celles qui diffèrent de l'état actuel.
    /// </summary>
    static List<(Cond cond, bool value)> Solve(CutsceneResolverData data)
    {
        var nodes = data.Nodes;
        List<(Cond, bool)> best = null;
        int bestCost = int.MaxValue;
        var path = new List<(Cond, bool)>();
        var visiting = new HashSet<int>();
        var cache = new Dictionary<int, (Cond cond, bool current)>();

        void Walk(int i, int cost)
        {
            if (i < 0 || i >= nodes.Length || cost >= bestCost || visiting.Contains(i) || visiting.Count > 80) return;
            var n = nodes[i];
            if (n == null) return;
            if (IsBossNode(n)) { bestCost = cost; best = new List<(Cond, bool)>(path); return; }
            visiting.Add(i);
            if (IsCondition(n))
            {
                if (!cache.TryGetValue(i, out var info))
                {
                    var c = ReadCond(n);
                    info = (c, c != null && Evaluate(c));
                    cache[i] = info;
                }
                var cond = n.TryCast<ResolverConditionNodeDataBase>();
                foreach (bool branch in new[] { true, false })
                {
                    var next = branch ? n.NextNodes : cond.ElseNodes;
                    if (next == null) continue;
                    // Condition inconnue : on ne peut pas la changer, on suppose qu'elle vaut… n'importe quoi (coût élevé)
                    int extra = info.cond == null ? 50 : info.current == branch ? 0 : 1;
                    if (info.cond != null) path.Add((info.cond, branch));
                    foreach (int j in next) Walk(j, cost + extra);
                    if (info.cond != null) path.RemoveAt(path.Count - 1);
                }
            }
            else if (n.NextNodes != null)
                foreach (int j in n.NextNodes) Walk(j, cost);
            visiting.Remove(i);
        }

        if (data.StartIndexes != null) foreach (int s in data.StartIndexes) Walk(s, 0);
        return best;
    }

    static void Force(CutsceneDirector director, Bosses.Boss boss)
    {
        var plan = Solve(director._resolverData);
        if (plan == null) { Plugin.Logger.LogInfo($"[Respawn] {boss.Name} : aucun chemin vers le combat dans '{director.name}'"); return; }
        applied.Clear();
        // Le scénario peut sauvegarder la partie pendant le combat forcé : copie des fichiers, remise au retour à la normale
        saveCopy = SaveGuard.Take();
        var done = new HashSet<string>();
        foreach (var (cond, value) in plan)
        {
            if (!done.Add(cond.Id) || Evaluate(cond) == value) continue;
            applied.Add(new Change { cond = cond, original = !value, originalStep = cond.kind == Kind.Arcana ? Step(cond.character) : default });
            Set(cond, value);
        }
        Plugin.Logger.LogInfo($"[Respawn] {boss.Name} : scénario '{director.name}', {plan.Count} condition(s) sur le chemin, changé : "
            + (applied.Count == 0 ? "rien" : string.Join(", ", applied.Select(c => $"{c.cond.Id} → {!c.original}"))));
        // Relance du scénario depuis son départ. ForceReset lève une erreur en fin de course, mais c'est avec lui
        // que le boss apparaît (sans lui, le scénario rejoue l'intro sans faire venir le boss).
        try { director.ForceReset(); }
        catch (Exception ex) { Plugin.Logger.LogInfo("[Respawn] ForceReset : " + ex.Message.Split('\n')[0]); }
        BossDiag.DumpDirectors("après relance du scénario");
        BossDiag.DumpRun(director, "juste après la relance");
        traced = director;
        traceTimes.Clear();
        foreach (float s in new[] { 1f, 3f, 6f }) traceTimes.Enqueue(Time.unscaledTime + s);
    }

    static CutsceneDirector traced;
    static readonly Queue<float> traceTimes = new();

    /// <summary>Relevés du déroulé quelques secondes après la relance (diagnostic).</summary>
    public static void UpdateTrace()
    {
        if (traced == null || traceTimes.Count == 0 || Time.unscaledTime < traceTimes.Peek()) return;
        traceTimes.Dequeue();
        BossDiag.DumpRun(traced, $"{traceTimes.Count switch { 2 => "1", 1 => "3", _ => "6" }} s après la relance");
        if (traceTimes.Count == 0) traced = null;
    }

    static Dictionary<string, byte[]> saveCopy;

    static void Revert()
    {
        SaveGuard.RestoreIfChanged(saveCopy, "pendant le combat forcé");
        saveCopy = null;
        if (applied.Count == 0) return;
        for (int i = applied.Count - 1; i >= 0; i--)
        {
            var c = applied[i];
            try { Set(c.cond, c.original, c.cond.kind == Kind.Arcana ? c.originalStep : CharacterProgressionStep.Unknown); }
            catch (Exception ex) { Plugin.Logger.LogWarning("[Respawn] remise en état : " + ex.Message); }
        }
        Plugin.Logger.LogInfo($"[Respawn] {activeBoss?.Name} : {applied.Count} changement(s) annulé(s)");
        applied.Clear();
    }
}
