using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using Il2CppInterop.Runtime;
using MotherBase.EntityFramework;
using UnityEngine;

namespace BelmontPractice;

/// <summary>
/// Fait vraiment revenir un boss dont le combat est piloté par un scénario (CutsceneDirector).
/// Le scénario de l'arène vérifie des conditions (clés de progression, arcanes absorbées, cinématiques vues)
/// avant d'arriver au lancement du combat ; et l'arène ne prépare le boss qu'à son chargement.
/// Donc tout se joue AVANT le chargement de l'arène :
///   1. à la première visite de l'arène, on lit son scénario et on retient les conditions qui mènent au combat
///      (scenarios_boss.json) ;
///   2. au TP vers un boss remis « Alive », on impose ces conditions, dans le jeu et dans la partie rechargée,
///      puis le niveau est rechargé en entier : l'arène se construit avec le boss, le scénario mène seul au combat ;
///   3. à la victoire (ou en ressortant sans combattre), tout est remis comme avant.
/// </summary>
public static class BossRespawn
{
    /// <summary>Boss remis « Alive » par l'onglet Bosses (CharacterID), dont le combat doit être rejoué.</summary>
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

    /// <summary>Condition retenue sur disque (par nom / identifiant, les objets du jeu n'étant pas toujours chargés).</summary>
    public class PlanCond
    {
        public string Kind { get; set; } = "";
        public string Name { get; set; } = "";
        public string Guid { get; set; } = "";
        public int Step { get; set; }
        public bool Value { get; set; }
    }

    static Bosses.Boss activeBoss, armedBoss;
    static string activeKey, armedKey; // rencontre (CharacterID@salle de l'arène) : un boss peut avoir deux arènes
    static bool armedEntered;
    static float searchUntil;
    static readonly List<Change> applied = new();
    static List<(Cond cond, bool value)> armedConds;
    static Dictionary<string, byte[]> saveCopy;

    // ---------- Conditions retenues par boss ----------

    static Dictionary<string, List<PlanCond>> plans;
    static string PlansPath => Path.Combine(SlotStore.Folder, "..", "scenarios_boss.json");

    static Dictionary<string, List<PlanCond>> Plans()
    {
        if (plans != null) return plans;
        plans = new Dictionary<string, List<PlanCond>>();
        try
        {
            if (File.Exists(PlansPath))
                plans = JsonSerializer.Deserialize<Dictionary<string, List<PlanCond>>>(File.ReadAllText(PlansPath)) ?? plans;
        }
        catch (Exception ex) { Plugin.Logger.LogWarning("[Respawn] lecture des scénarios : " + ex.Message); }
        return plans;
    }

    static void StorePlan(Bosses.Boss boss, List<(Cond cond, bool value)> conds)
    {
        var list = new List<PlanCond>();
        var seen = new HashSet<string>();
        foreach (var (c, v) in conds)
        {
            if (!seen.Add(c.Id)) continue;
            list.Add(c.kind switch
            {
                Kind.Key => new PlanCond { Kind = "Key", Name = c.key.name, Guid = c.key.guid, Value = v },
                Kind.Arcana => new PlanCond { Kind = "Arcana", Name = c.character.name, Guid = c.character.guid, Step = (int)c.step, Value = v },
                _ => new PlanCond { Kind = "Cutscene", Guid = c.cutsceneId, Value = v },
            });
        }
        Plans()[activeKey ?? boss.Id.name] = list;
        try { File.WriteAllText(PlansPath, JsonSerializer.Serialize(plans, new JsonSerializerOptions { WriteIndented = true })); }
        catch (Exception ex) { Plugin.Logger.LogWarning("[Respawn] écriture des scénarios : " + ex.Message); }
        Plugin.Logger.LogInfo($"[Respawn] {boss.Name} : conditions du combat retenues : {string.Join(", ", list.Select(p => $"{p.Kind} {(p.Name.Length > 0 ? p.Name : p.Guid)}={p.Value}"))}");
    }

    static T FindLoaded<T>(Func<T, bool> match) where T : UnityEngine.Object
    {
        foreach (var o in Resources.FindObjectsOfTypeAll(Il2CppType.Of<T>()))
        {
            var x = o.TryCast<T>();
            if (x != null && match(x)) return x;
        }
        return null;
    }

    /// <summary>Conditions retenues, en objets du jeu (null si le boss n'a pas encore été vu, ou si une clé est introuvable).</summary>
    static List<(Cond cond, bool value)> LoadPlan(Bosses.Boss boss, string key)
    {
        // Conditions de cette rencontre ; à défaut, l'ancien format (une seule arène par boss)
        if ((key == null || !Plans().TryGetValue(key, out var list) || list == null || list.Count == 0)
            && (!Plans().TryGetValue(boss.Id.name, out list) || list == null || list.Count == 0)) return null;
        var res = new List<(Cond, bool)>();
        foreach (var p in list)
        {
            switch (p.Kind)
            {
                case "Key":
                    var pkey = FindLoaded<ProgressionKeyID>(k => (p.Guid.Length > 0 && k.guid == p.Guid) || k.name == p.Name);
                    if (pkey == null) { Plugin.Logger.LogWarning($"[Respawn] {boss.Name} : clé '{p.Name}' introuvable"); return null; }
                    res.Add((new Cond { kind = Kind.Key, key = pkey }, p.Value));
                    break;
                case "Arcana":
                    var ch = Bosses.All.FirstOrDefault(b => b.Id?.name == p.Name)?.Id
                        ?? FindLoaded<CharacterID>(c => c.name == p.Name);
                    if (ch == null) { Plugin.Logger.LogWarning($"[Respawn] {boss.Name} : personnage '{p.Name}' introuvable"); return null; }
                    res.Add((new Cond { kind = Kind.Arcana, character = ch, step = (CharacterProgressionStep)p.Step }, p.Value));
                    break;
                default:
                    res.Add((new Cond { kind = Kind.Cutscene, cutsceneId = p.Guid }, p.Value));
                    break;
            }
        }
        return res;
    }

    // ---------- Avant le TP ----------

    /// <summary>
    /// Juste avant le TP vers un boss remis « Alive » : impose les conditions retenues (dans le jeu).
    /// Retourne false si elles ne sont pas encore connues (il faut être entré une fois dans l'arène).
    /// </summary>
    public static bool Arm(Bosses.Boss boss, string key)
    {
        armedBoss = null;
        armedKey = null;
        armedConds = null;
        var conds = LoadPlan(boss, key);
        if (conds == null) { Plugin.Logger.LogInfo($"[Respawn] {boss.Name} : conditions du combat pas encore connues"); return false; }
        Revert(); // un éventuel forçage précédent
        saveCopy = SaveGuard.Take(); // le scénario peut sauvegarder la partie pendant le combat
        var done = new HashSet<string>();
        foreach (var (c, v) in conds)
        {
            if (!done.Add(c.Id) || Evaluate(c) == v) continue;
            applied.Add(new Change { cond = c, original = !v, originalStep = c.kind == Kind.Arcana ? Step(c.character) : default });
            Set(c, v);
        }
        armedBoss = boss;
        armedKey = key;
        armedEntered = false;
        armedConds = conds;
        activeBoss = boss;
        activeKey = key;
        Plugin.Logger.LogInfo($"[Respawn] {boss.Name} : prêt avant le TP, changé : "
            + (applied.Count == 0 ? "rien" : string.Join(", ", applied.Select(c => $"{c.cond.Id} → {!c.original}"))));
        return true;
    }

    /// <summary>
    /// La partie que la téléportation recharge est une copie (JSON) : les conditions y sont écrites aussi,
    /// sinon le chargement du niveau relirait l'ancien état.
    /// </summary>
    public static void PatchSave(SaveState state)
    {
        if (state == null) return;
        if (armedConds != null && state.GlobalSaveJson != null) try
        {
            var root = JsonNode.Parse(state.GlobalSaveJson);
            var ps = root?["playerSave"];
            if (ps == null) return;
            var changes = new List<string>();
            foreach (var (c, v) in armedConds)
            {
                switch (c.kind)
                {
                    case Kind.Key:
                        if (SetInArray(ps["progressionPersistentSave"] as JsonArray, c.key.guid, v)) changes.Add($"{c.key.name}={v}");
                        break;
                    case Kind.Cutscene:
                        if (SetInArray(ps["watchedCutscenes"] as JsonArray, c.cutsceneId, v)) changes.Add($"cinématique {c.cutsceneId}={v}");
                        break;
                    case Kind.Arcana:
                        var dict = ps["characterProgressionSteps"];
                        var keys = dict?["m_keys"] as JsonArray;
                        var vals = dict?["m_values"] as JsonArray;
                        if (keys == null || vals == null) break;
                        int idx = -1;
                        for (int i = 0; i < keys.Count; i++) if ((string)keys[i] == c.character.guid) { idx = i; break; }
                        int cur = idx >= 0 ? (int)vals[idx] : 0;
                        int target = v ? Math.Max(cur, (int)c.step) : Math.Min(cur, (int)c.step - 1);
                        if (target == cur) break;
                        if (idx >= 0) vals[idx] = target; else { keys.Add(c.character.guid); vals.Add(target); }
                        changes.Add($"{c.character.name} {cur}→{target}");
                        break;
                }
            }
            state.GlobalSaveJson = root.ToJsonString();
            Plugin.Logger.LogInfo("[Respawn] partie rechargée : " + (changes.Count == 0 ? "déjà conforme" : string.Join(", ", changes)));
        }
        catch (Exception ex) { Plugin.Logger.LogWarning("[Respawn] partie rechargée : " + ex.Message); }

        // Mémoire temporaire du niveau (ennemis tués, spawners déjà vidés… : ce qu'un repos au miroir efface).
        // Après une victoire, le spawner du boss y est noté comme vidé : il ne recrée plus le boss. On la vide,
        // comme un passage au miroir (les ennemis du niveau réapparaissent).
        try
        {
            if (string.IsNullOrEmpty(state.RuntimeWorldSaveJson)) return;
            var rw = JsonNode.Parse(state.RuntimeWorldSaveJson);
            var ses = rw?["savableEntityStates"];
            var keys = ses?["m_keys"] as JsonArray;
            var vals = ses?["m_values"] as JsonArray;
            int n = keys?.Count ?? 0;
            keys?.Clear();
            vals?.Clear();
            state.RuntimeWorldSaveJson = rw?.ToJsonString();
            Plugin.Logger.LogInfo($"[Respawn] mémoire temporaire du niveau vidée ({n} état(s)), comme au miroir");
        }
        catch (Exception ex) { Plugin.Logger.LogWarning("[Respawn] mémoire temporaire du niveau : " + ex.Message); }
    }

    /// <summary>Présence (v=true) ou absence (v=false) d'une valeur dans un tableau JSON. Retourne true si modifié.</summary>
    static bool SetInArray(JsonArray arr, string value, bool v)
    {
        if (arr == null || string.IsNullOrEmpty(value)) return false;
        bool changed = false;
        for (int i = arr.Count - 1; i >= 0; i--)
            if ((string)arr[i] == value) { if (v) return false; arr.RemoveAt(i); changed = true; }
        if (v) { arr.Add(value); changed = true; }
        return changed;
    }

    // ---------- Arène ----------

    /// <summary>Entrée dans l'arène d'une rencontre (key = CharacterID@salle de l'arène).</summary>
    public static void OnEnterArena(Bosses.Boss boss, string key)
    {
        if (boss?.Id == null || !Pending.Contains(boss.Id.name)) return;
        if (Bosses.IsDefeated(boss) == true) { Pending.Remove(boss.Id.name); return; }
        if (armedBoss == boss) armedEntered = true;
        activeBoss = boss;
        activeKey = key;
        // Le scénario n'existe qu'une fois la salle chargée : on le lit pour vérifier / apprendre les conditions
        searchUntil = Time.unscaledTime + 6f;
    }

    public static void OnLeaveArena()
    {
        // Rechargement du TP lui-même (pas encore entré dans l'arène) : les conditions doivent rester en place
        if (armedBoss != null && !armedEntered) return;
        searchUntil = 0f;
        if (activeBoss == null) return;
        if (Bosses.IsDefeated(activeBoss) == true) Pending.Remove(activeBoss.Id.name);
        Revert();
        activeBoss = null;
        activeKey = null;
        armedBoss = null;
        armedKey = null;
        armedConds = null;
    }

    /// <summary>
    /// Fin de la rencontre : marque « vaincu » du boss, ou marque propre à la rencontre parmi les conditions imposées
    /// (Death I : DeathCatacombs_Defeated_ID, que le combat repose à la fin).
    /// </summary>
    static bool EncounterDone()
    {
        if (Bosses.IsDefeated(activeBoss) == true) return true;
        foreach (var c in applied)
            if (c.cond.kind == Kind.Key && c.original && c.cond.key != null
                && c.cond.key.name.IndexOf("Defeated", StringComparison.OrdinalIgnoreCase) >= 0 && Evaluate(c.cond))
                return true;
        return false;
    }

    // ---------- Après le combat ----------

    static float watchUntil, lockSince = -1f, dumpAfterAt;
    static string watchRoom;
    static int lockId = -1;

    /// <summary>
    /// Après un combat forcé, la suite de l'histoire peut ne plus se jouer (Death I : le sauvetage par Trevor est déjà
    /// passé) et laisser les menus verrouillés (GameplayLockMenuState) sans rien par-dessus. Ce verrou orphelin est
    /// retiré s'il reste seul au sommet 6 s d'affilée.
    /// </summary>
    static void WatchAfterFight()
    {
        if (watchUntil <= 0f) return;
        float now = Time.unscaledTime;
        var gsm = GameStateManager.GetInstance();
        var states = gsm?._states;
        if (states == null || states.Count == 0) { watchUntil = 0f; return; }
        var top = states.Peek();
        string name = top?.Item2?.name ?? "";
        if (name == "GameplayLockMenuState")
        {
            if (lockId != top.Item1) { lockId = top.Item1; lockSince = now; }
            else if (now - lockSince >= 6f)
            {
                gsm.PopState(top.Item1, gsm);
                Plugin.Logger.LogInfo($"[Respawn] après le combat : verrou des menus orphelin retiré (id {top.Item1})");
                lockId = -1;
            }
        }
        else lockId = -1;
        // Diagnostic : scénarios chargés après le combat (la suite de l'histoire, ex. sauvetage par Trevor après Death I),
        // 3 s après chaque changement de salle
        string room = GameBridge.CurrentRoomId();
        if (room != watchRoom) { watchRoom = room; dumpAfterAt = now + 3f; }
        if (dumpAfterAt > 0f && now >= dumpAfterAt) { dumpAfterAt = 0f; BossDiag.DumpDirectors("après le combat", all: true); }
        if (now > watchUntil)
        {
            watchUntil = 0f;
            var names = new List<string>();
            foreach (var t in states.ToArray()) names.Add($"{t?.Item2?.name}#{t?.Item1}");
            Plugin.Logger.LogInfo("[Respawn] après le combat, états du jeu : " + string.Join(" > ", names));
        }
    }

    public static void Update()
    {
        try { WatchAfterFight(); } catch (Exception ex) { watchUntil = 0f; Plugin.Logger.LogWarning("[Respawn] après le combat : " + ex.Message); }
        try { UpdateTrace(); } catch (Exception ex) { traced = null; Plugin.Logger.LogWarning("[Respawn] relevé : " + ex.Message); }
        // Boss battu : la progression modifiée (porte, clés…) est remise aussitôt, avant que le scénario
        // ne reparte de son début (il retrouve ainsi l'état d'avant le combat).
        if (activeBoss != null && applied.Count > 0 && (armedBoss == null || armedEntered) && EncounterDone())
        {
            Plugin.Logger.LogInfo($"[Respawn] {activeBoss.Name} battu : progression d'avant le combat remise");
            watchUntil = Time.unscaledTime + 120f; // la cinématique de fin et le changement de salle peuvent être longs
            dumpAfterAt = Time.unscaledTime + 8f; // après la cinématique de fin et le changement de salle éventuel
            Revert();
            Pending.Remove(activeBoss.Id.name);
            armedBoss = null;
            armedConds = null;
        }
        if (searchUntil <= 0f || activeBoss == null) return;
        if (Time.unscaledTime > searchUntil)
        {
            searchUntil = 0f;
            // Combat lancé par un simple déclencheur (BossFightTrigger) plutôt que par un scénario ?
            var triggers = new List<string>();
            try
            {
                foreach (var o in Resources.FindObjectsOfTypeAll(Il2CppType.Of<BossFightTrigger>()))
                {
                    var t = o.TryCast<BossFightTrigger>();
                    if (t == null || !t.gameObject.scene.IsValid()) continue;
                    string defeated;
                    try { defeated = t.IsBossDefeated().ToString(); } catch (Exception ex) { defeated = "?" + ex.Message; }
                    triggers.Add($"'{t.name}' boss={t.bossData?.name ?? "?"} actif={t.gameObject.activeInHierarchy} vaincu={defeated} déclenché={t.encounterHasBeenTriggered} rencontre={t.encounterNumber}");
                }
            }
            catch { }
            Plugin.Logger.LogInfo($"[Respawn] {activeBoss.Name} : aucun scénario de combat trouvé dans l'arène ; déclencheurs : {(triggers.Count == 0 ? "aucun" : string.Join(" | ", triggers))}");
            return;
        }
        var director = FindDirector(activeBoss);
        if (director == null) return;
        searchUntil = 0f;
        try { Check(director, activeBoss); }
        catch (Exception ex) { Plugin.Logger.LogWarning($"[Respawn] {activeBoss.Name} : {ex}"); }
    }

    // ---------- Scénario de l'arène ----------

    static bool IsBossNode(NodeDataBase n)
    {
        string t = n?.GetIl2CppType().Name ?? "";
        // Combat factice (le boss s'enfuit / combat scripté) : InitFakeBoss
        return t == "ResolverSetupBossNodeData" || t == "ResolverStartBossFightNodeData" || t == "ResolverInitFakeBossNodeData";
    }

    /// <summary>
    /// Nœud qui mène au combat : mise en place / début du combat, ou sous-scénario qui en contient un
    /// (Isaac I : le combat démarre dans « BossIntroNoTrigger », sans mise en place dans le scénario principal).
    /// </summary>
    static bool LeadsToFight(NodeDataBase n)
    {
        if (IsBossNode(n)) return true;
        var sub = n?.TryCast<ResolverPlaySubResolverNodeData>()?.SubResolverData?.Nodes;
        if (sub == null) return false;
        foreach (var sn in sub) if (IsBossNode(sn)) return true;
        return false;
    }

    /// <summary>Le nœud cite-t-il le boss ? (action de mise en place « LEON_… », ou personnage passé au sous-scénario)</summary>
    static bool Mentions(NodeDataBase n, Bosses.Boss boss, string shortName)
    {
        string action = n.TryCast<ResolverSetupBossNodeData>()?.ActionData?.Value?.name ?? "";
        if (action.IndexOf(shortName, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        var vars = n.TryCast<ResolverPlaySubResolverNodeData>()?.VariablesContainer?._variables;
        if (vars == null) return false;
        for (int i = 0; i < vars.Count; i++)
        {
            try
            {
                var real = BossDiag.Real(vars[i]);
                var value = real.GetType().GetProperty("Value")?.GetValue(real) as Il2CppSystem.Object;
                if (value?.TryCast<CharacterID>()?.name == boss.Id.name) return true;
            }
            catch { }
        }
        return false;
    }

    /// <summary>
    /// Scénario du combat du boss : celui qui cite le boss, sinon le scénario de boss le plus proche du joueur
    /// (il est dans l'arène).
    /// </summary>
    static CutsceneDirector FindDirector(Bosses.Boss boss)
    {
        string shortName = boss.Id.name.Replace("CharacterID_", "");
        CutsceneDirector nearest = null;
        float best = float.MaxValue;
        var player = GameBridge.PlayerPosition();
        var seen = new List<string>();
        foreach (var o in Resources.FindObjectsOfTypeAll(Il2CppType.Of<CutsceneDirector>()))
        {
            var d = o.TryCast<CutsceneDirector>();
            if (d == null || !d.gameObject.scene.IsValid() || !d.isActiveAndEnabled) continue;
            var nodes = d._resolverData?.Nodes;
            if (nodes == null) continue;
            foreach (var n in nodes)
            {
                if (!LeadsToFight(n)) continue;
                if (Mentions(n, boss, shortName)) return d;
                float dist = Vector3.Distance(d.transform.position, player);
                seen.Add($"'{d.name}' à {dist:0}");
                if (dist < best) { best = dist; nearest = d; }
                break;
            }
        }
        if (seen.Count > 1) Plugin.Logger.LogInfo($"[Respawn] {boss.Name} : {seen.Count} scénarios de boss chargés ({string.Join(", ", seen)}), le plus proche est retenu");
        return nearest;
    }

    /// <summary>Valeur d'une variable de scénario (sous-scénarios génériques : la valeur vient du scénario parent).</summary>
    static T RefValue<T>(VariableRef r, VariablesContainer vars) where T : Il2CppSystem.Object
    {
        try
        {
            if (r == null || vars == null) return null;
            var v = vars.GetVariable(r.Guid);
            if (v == null) return null;
            var real = BossDiag.Real(v);
            return (real.GetType().GetProperty("Value")?.GetValue(real) as Il2CppSystem.Object)?.TryCast<T>();
        }
        catch { return null; }
    }

    static Cond ReadCond(NodeDataBase n, VariablesContainer vars = null)
    {
        var pk = n.TryCast<ResolverConditionProgressionKeyNodeData>();
        if (pk != null)
        {
            var key = pk.ProgressionKey == null ? null : pk.ProgressionKey.UseRef ? RefValue<ProgressionKeyID>(pk.ProgressionKey.ValueRef, vars) : pk.ProgressionKey.Value;
            return key == null ? null : new Cond { kind = Kind.Key, key = key };
        }
        var ar = n.TryCast<ResolverConditionArcanaStateNodeData>();
        if (ar != null)
        {
            var ch = ar.Character == null ? null : ar.Character.UseRef ? RefValue<CharacterID>(ar.Character.ValueRef, vars) : ar.Character.Value;
            return ch == null ? null : new Cond { kind = Kind.Arcana, character = ch, step = ar.ArcanaState };
        }
        var wc = n.TryCast<ResolverConditionHasWatchedCutsceneNodeData>();
        if (wc != null)
        {
            var cs = wc.CutsceneData == null ? null : wc.CutsceneData.UseRef ? RefValue<CutsceneData>(wc.CutsceneData.ValueRef, vars) : wc.CutsceneData.Value;
            return cs == null ? null : new Cond { kind = Kind.Cutscene, cutsceneId = cs.ID };
        }
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
            if (LeadsToFight(n)) { bestCost = cost; best = new List<(Cond, bool)>(path); return; }
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
                    // Condition illisible : on ne peut pas la changer (coût élevé)
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

    /// <summary>
    /// Toutes les conditions qui mènent au combat : le chemin du scénario principal, plus « cinématique d'entrée
    /// non vue » dans les sous-scénarios (déjà vue, le combat démarre sans la cinématique qui fait apparaître le boss).
    /// </summary>
    static List<(Cond cond, bool value)> Conditions(CutsceneDirector director, Bosses.Boss boss)
    {
        var plan = Solve(director._resolverData);
        if (plan == null) return null;
        var all = new List<(Cond, bool)>(plan);
        all.AddRange(SpawnerConditions(boss));
        foreach (var n in director._resolverData.Nodes)
        {
            var play = n?.TryCast<ResolverPlaySubResolverNodeData>();
            var sub = play?.SubResolverData?.Nodes;
            if (sub == null) continue;
            foreach (var sn in sub)
            {
                if (sn?.TryCast<ResolverConditionHasWatchedCutsceneNodeData>() == null) continue;
                var cond = ReadCond(sn, play.VariablesContainer);
                if (cond != null) all.Add((cond, false));
            }
        }
        return all;
    }

    /// <summary>
    /// Spawners qui créent le boss (Isaac I : « IsaacIntro_BossData » avec Carmilla) : leur condition d'apparition
    /// (clé de progression à avoir / ne pas avoir, éventuellement inversée). Elle est évaluée au chargement de la salle,
    /// d'où son imposition avant le TP.
    /// </summary>
    static List<(Cond cond, bool value)> SpawnerConditions(Bosses.Boss boss)
    {
        var res = new List<(Cond, bool)>();
        string shortName = boss.Id.name.Replace("CharacterID_", "");
        foreach (var o in Resources.FindObjectsOfTypeAll(Il2CppType.Of<MobsSpawner>()))
        {
            try
            {
                var s = o.TryCast<MobsSpawner>();
                if (s == null || !s.gameObject.scene.IsValid() || s.spawnCondition == null) continue;
                bool spawnsBoss = false;
                var mobs = s.mobsToSpawn;
                if (mobs != null)
                    for (int i = 0; i < mobs.Count; i++)
                        if ((mobs[i].mobData?.name ?? "").IndexOf(shortName, StringComparison.OrdinalIgnoreCase) >= 0) spawnsBoss = true;
                if (!spawnsBoss) continue;
                // Condition éventuellement inversée (EntityConditionNot)
                EntityConditionAbstract c = s.spawnCondition;
                bool invert = false;
                for (int depth = 0; depth < 4; depth++)
                {
                    var not = c?.TryCast<EntityConditionNot>();
                    if (not == null) break;
                    invert = !invert;
                    c = not.condition;
                }
                var pk = c?.TryCast<EntityConditionCheckProgressKey>();
                if (pk?.progressionKeyToCheck == null)
                {
                    Plugin.Logger.LogInfo($"[Respawn] {boss.Name} : spawner '{s.name}', condition non gérée ({c?.GetIl2CppType().Name ?? "?"})");
                    continue;
                }
                bool want = pk.shouldHave != invert;
                res.Add((new Cond { kind = Kind.Key, key = pk.progressionKeyToCheck }, want));
                Plugin.Logger.LogInfo($"[Respawn] {boss.Name} : spawner '{s.name}' demande la clé {pk.progressionKeyToCheck.name}={want}");
            }
            catch (Exception ex) { Plugin.Logger.LogWarning("[Respawn] spawner : " + ex.Message); }
        }
        return res;
    }

    /// <summary>
    /// Dans l'arène : lit le scénario, retient ses conditions pour les prochains TP, et vérifie qu'elles sont en place.
    /// Si elles ne l'étaient pas (entrée sans TP, conditions encore inconnues), on les impose maintenant : cela ne
    /// suffit que si l'arène n'avait pas encore été chargée — le TP suivant, lui, fonctionnera.
    /// </summary>
    static void Check(CutsceneDirector director, Bosses.Boss boss)
    {
        var conds = Conditions(director, boss);
        if (conds == null) { Plugin.Logger.LogInfo($"[Respawn] {boss.Name} : aucun chemin vers le combat dans '{director.name}'"); return; }
        StorePlan(boss, conds);
        var missing = conds.Where(c => Evaluate(c.cond) != c.value).ToList();
        if (missing.Count == 0)
        {
            Plugin.Logger.LogInfo($"[Respawn] {boss.Name} : conditions en place, le scénario mène au combat");
        }
        else if (activeKey != null && (autoReloadKey != activeKey || Time.unscaledTime - autoReloadAt > 30f))
        {
            // Conditions découvertes trop tard (l'arène est déjà chargée sans le boss) : on les impose et on recharge
            // le niveau en entier, le joueur restant où il est. L'arène se reconstruit avec le boss, sans autre action.
            autoReloadKey = activeKey;
            autoReloadAt = Time.unscaledTime;
            Plugin.Logger.LogInfo($"[Respawn] {boss.Name} : conditions manquantes ({string.Join(", ", missing.Select(c => $"{c.cond.Id} → {c.value}"))}), rechargement du niveau sur place");
            Arenas.RequestReloadHere(boss, activeKey);
        }
        else
        {
            saveCopy ??= SaveGuard.Take();
            var done = new HashSet<string>();
            foreach (var (c, v) in missing)
            {
                if (!done.Add(c.Id)) continue;
                applied.Add(new Change { cond = c, original = !v, originalStep = c.kind == Kind.Arcana ? Step(c.character) : default });
                Set(c, v);
            }
            Plugin.Logger.LogInfo($"[Respawn] {boss.Name} : conditions imposées à l'entrée (sans TP) : {string.Join(", ", missing.Select(c => $"{c.cond.Id} → {c.value}"))}"
                + " — si le boss n'apparaît pas, ressors, remets-le « Alive » et utilise TP");
        }
        if (!Plugin.BossDiagnostics.Value) return;
        BossDiag.DumpActors("à l'entrée", boss.Id.name.Replace("CharacterID_", ""));
        traced = director;
        traceTimes.Clear();
        foreach (float s in new[] { 1f, 3f, 6f }) traceTimes.Enqueue(Time.unscaledTime + s);
    }

    static string autoReloadKey;
    static float autoReloadAt = -100f;

    static CutsceneDirector traced;
    static readonly Queue<float> traceTimes = new();

    /// <summary>Relevés du déroulé quelques secondes après l'entrée (diagnostic).</summary>
    public static void UpdateTrace()
    {
        if (traced == null || traceTimes.Count == 0 || Time.unscaledTime < traceTimes.Peek()) return;
        traceTimes.Dequeue();
        string when = $"{traceTimes.Count switch { 2 => "1", 1 => "3", _ => "6" }} s après l'entrée";
        BossDiag.DumpRun(traced, when);
        BossDiag.DumpActors(when, activeBoss?.Id?.name.Replace("CharacterID_", ""));
        if (traceTimes.Count == 0) traced = null;
    }

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
