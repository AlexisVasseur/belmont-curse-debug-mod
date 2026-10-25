using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using UnityEngine;

namespace BelmontPractice;

/// <summary>
/// Diagnostic des combats de boss pilotés par cinématique (CutsceneDirector + graphe « resolver ») :
/// écrit dans le journal chaque nœud du graphe (type, champs, suites) et les nœuds en cours.
/// </summary>
public static class BossDiag
{
    static readonly Dictionary<string, Type> typeCache = new();
    static readonly MethodInfo tryCast = typeof(Il2CppObjectBase).GetMethod("TryCast");

    /// <summary>Objet Il2Cpp converti vers son vrai type managé (sinon tel quel).</summary>
    public static object Real(Il2CppObjectBase o)
    {
        try
        {
            string name = o.Cast<Il2CppSystem.Object>().GetIl2CppType().FullName;
            if (!typeCache.TryGetValue(name, out var t))
            {
                t = typeof(BossData).Assembly.GetType(name.Replace('+', '+')) ?? typeof(BossData).Assembly.GetTypes().FirstOrDefault(x => x.FullName == name);
                typeCache[name] = t;
            }
            if (t == null) return o;
            return tryCast.MakeGenericMethod(t).Invoke(o, null) ?? o;
        }
        catch { return o; }
    }

    static string Short(object v, int depth = 0)
    {
        try
        {
            switch (v)
            {
                case null: return "null";
                case string s: return '"' + s + '"';
                case UnityEngine.Object u: return u == null ? "null" : $"'{u.name}'";
                case Il2CppObjectBase o when depth < 2:
                {
                    var real = Real(o);
                    var t = real.GetType();
                    // VariableWithRefData<V,R> : valeur directe ou référence de variable
                    var pv = t.GetProperty("Value"); var pu = t.GetProperty("UseRef"); var pr = t.GetProperty("ValueRef");
                    if (pv != null && pu != null)
                        return (bool)pu.GetValue(real) ? $"ref:{Short(pr?.GetValue(real), depth + 1)}" : Short(pv.GetValue(real), depth + 1);
                    if (real is Il2CppSystem.Object so && t.Name.Contains("Array"))
                        return t.Name;
                    var parts = t.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                        .Where(p => p.GetIndexParameters().Length == 0 && p.Name != "ObjectClass" && p.Name != "Pointer" && p.Name != "WasCollected")
                        .Take(6).Select(p => { try { return p.Name + "=" + Short(p.GetValue(real), depth + 1); } catch { return p.Name + "=?"; } });
                    return t.Name + "{" + string.Join(", ", parts) + "}";
                }
                case Il2CppObjectBase o2: return o2.Cast<Il2CppSystem.Object>().GetIl2CppType().Name;
                default: return v.ToString();
            }
        }
        catch (Exception ex) { return "?" + ex.Message; }
    }

    static string Try(Func<bool> f) { try { return f().ToString(); } catch (Exception ex) { return "?" + ex.Message; } }

    static string Ints(Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<int> a) =>
        a == null || a.Length == 0 ? "-" : string.Join(",", a.ToArray());

    /// <summary>
    /// Déroulé réel du scénario : chaque nœud d'exécution atteint (démarré ou non) et son journal interne,
    /// y compris les sous-scénarios (BossIntro…).
    /// </summary>
    public static void DumpRun(CutsceneDirector d, string when)
    {
        try
        {
            var sb = new StringBuilder();
            var seen = new HashSet<IntPtr>();
            void Walk(ResolverNodeBase n, string indent, int depth)
            {
                if (n == null || depth > 6 || !seen.Add(n.Pointer)) return;
                var real = Real(n);
                string type = real.GetType().Name;
                var logs = new List<string>();
                try { var l = n.Logs; if (l != null) for (int i = 0; i < l.Count; i++) logs.Add($"{l[i].Severity}:{l[i].Message}"); } catch { }
                sb.AppendLine($"{indent}[{n.Index}] {type} commencé={n.HasBegun}" + (logs.Count > 0 ? " journal: " + string.Join(" | ", logs) : ""));
                // Sous-scénario
                try
                {
                    var sub = real.GetType().GetProperty("_resolver")?.GetValue(real) as CutsceneResolver;
                    if (sub != null) { var sn = sub._startNodes; if (sn != null) for (int i = 0; i < sn.Count; i++) Walk(sn[i], indent + "    ", depth + 1); }
                }
                catch { }
                var nexts = new List<ResolverNodeBase>();
                try { var a = n._nextNodes; if (a != null) foreach (var x in a) nexts.Add(x); } catch { }
                try { if (real.GetType().GetProperty("_elseNodes")?.GetValue(real) is Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<ResolverNodeBase> e) foreach (var x in e) nexts.Add(x); } catch { }
                foreach (var x in nexts) Walk(x, indent, depth);
            }
            var r = d._resolver;
            var starts = r?._startNodes;
            if (starts != null) for (int i = 0; i < starts.Count; i++) Walk(starts[i], "    ", 0);
            var cur = new List<int>();
            try { var cn = r?._currentNodes; if (cn != null) for (int i = 0; i < cn.Count; i++) cur.Add(cn[i].Index); } catch { }
            Plugin.Logger.LogInfo($"[Boss] déroulé de '{d.name}' ({when}), en cours=[{string.Join(",", cur)}]\n{sb}");
        }
        catch (Exception ex) { Plugin.Logger.LogWarning("[Boss] déroulé : " + ex); }
    }

    /// <summary>Nœuds d'un graphe (et, pour les sous-scénarios, leurs variables et leurs propres nœuds).</summary>
    static void DumpNodes(StringBuilder sb, Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<NodeDataBase> nodes, string indent, int depth)
    {
        for (int i = 0; i < nodes.Length; i++)
        {
            var n = nodes[i];
            if (n == null) continue;
            var real = Real(n);
            var t = real.GetType();
            var fields = new List<string>();
            for (var tt = t; tt != null && tt.Name != "ResolverNodeDataBase" && tt.Name != "NodeDataBase" && tt != typeof(object); tt = tt.BaseType)
                foreach (var p in tt.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    if (p.GetIndexParameters().Length > 0 || p.Name == "ElseNodes" || p.Name == "IsUniquePerBranch" || p.Name == "VariablesContainer") continue;
                    try { fields.Add(p.Name + "=" + Short(p.GetValue(real))); } catch { }
                }
            string els = "";
            try { if (real.GetType().GetProperty("ElseNodes")?.GetValue(real) is Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<int> e) els = " sinon→" + Ints(e); } catch { }
            sb.AppendLine($"{indent}[{i}] {t.Name.Replace("NodeData", "")} {string.Join(" ", fields)} →{Ints(n.NextNodes)}{els}");

            var play = n.TryCast<ResolverPlaySubResolverNodeData>();
            if (play == null || depth > 2) continue;
            try
            {
                var vars = play.VariablesContainer?._variables;
                if (vars != null)
                    for (int v = 0; v < vars.Count; v++)
                    {
                        var rv = Real(vars[v]);
                        sb.AppendLine($"{indent}      var {vars[v].Guid} {rv.GetType().Name} = {Short(rv.GetType().GetProperty("Value")?.GetValue(rv))}");
                    }
                var subNodes = play.SubResolverData?.Nodes;
                if (subNodes != null) DumpNodes(sb, subNodes, indent + "      ", depth + 1);
            }
            catch (Exception ex) { sb.AppendLine($"{indent}      (sous-scénario illisible : {ex.Message})"); }
        }
    }

    /// <summary>Registre des acteurs de cinématique (CutsceneManager), acteurs présents dans la scène et boss en cours.</summary>
    public static void DumpActors(string when, string characterFilter = null)
    {
        try
        {
            var sb = new StringBuilder();
            var dict = CutsceneManager.Instance?._cutsceneActors;
            if (dict != null)
                foreach (var kv in dict)
                {
                    string key = kv.Key?.name ?? "null";
                    if (characterFilter != null && key.IndexOf(characterFilter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    var a = kv.Value;
                    bool alive = a != null && a.Cast<UnityEngine.Object>() != null;
                    sb.AppendLine($"  registre '{key}' → {(alive ? $"'{a.name}' actif={a.gameObject.activeInHierarchy}" : "DÉTRUIT")}");
                }
            foreach (var o in Resources.FindObjectsOfTypeAll(Il2CppType.Of<CutsceneActor>()))
            {
                var a = o.TryCast<CutsceneActor>();
                if (a == null || !a.gameObject.scene.IsValid()) continue;
                string cd = a._characterData?.name ?? "?";
                if (characterFilter != null && cd.IndexOf(characterFilter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                sb.AppendLine($"  scène '{a.name}' perso={cd} actif={a.gameObject.activeInHierarchy} pos={a.transform.position}");
            }
            // Spawners de mobs qui citent le boss : ce sont eux qui créent son entité
            MotherBase.EntityFramework.Entity player = null;
            try { MainCharacterSingleton.TryGetEntity(out player); } catch { }
            foreach (var o in Resources.FindObjectsOfTypeAll(Il2CppType.Of<MobsSpawner>()))
            {
                var s = o.TryCast<MobsSpawner>();
                if (s == null || !s.gameObject.scene.IsValid()) continue;
                var mobs = new List<string>();
                try { var l = s.mobsToSpawn; if (l != null) for (int i = 0; i < l.Count; i++) mobs.Add(l[i].mobData?.name ?? "?"); } catch { }
                string all = string.Join(",", mobs) + " " + s.name;
                if (characterFilter != null && all.IndexOf(characterFilter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                string cond = "aucune";
                try { if (s.spawnCondition != null) cond = $"{s.spawnCondition.GetIl2CppType().Name}={(player != null ? s.spawnCondition.IsConditionValidated(player).ToString() : "?")}"; } catch (Exception ex) { cond = "? " + ex.Message; }
                string pd = "?";
                try { var p = s.persistentData; pd = $"aApparu={p.HasSpawned} état={p.State}"; } catch { }
                sb.AppendLine($"  spawner '{s.name}' actif={s.isActiveAndEnabled} init={s.initialized} mobs=[{string.Join(",", mobs)}] {pd} condition={cond} apparus={s.spawnedMobsCount} morts={s.deadMobsCount} vivants={s.aliveSpawnedMobs?.Count}");
            }
            string boss = "?";
            try { boss = BossManager.Get()?.currentBossVariable?.CurrentValue?.name ?? "aucun"; } catch { }
            Plugin.Logger.LogInfo($"[Boss] acteurs ({when}) : boss en cours={boss}\n{sb}");
        }
        catch (Exception ex) { Plugin.Logger.LogWarning("[Boss] acteurs : " + ex.Message); }
    }

    /// <summary>Condition d'entité en clair : clés de progression (à avoir / pas), inversions, sous-conditions.</summary>
    public static string Describe(EntityConditionAbstract c, MotherBase.EntityFramework.Entity player, int depth = 0)
    {
        if (c == null) return "aucune";
        if (depth > 4) return "…";
        try
        {
            var not = c.TryCast<EntityConditionNot>();
            if (not != null) return $"NON({Describe(not.condition, player, depth + 1)})";
            var pk = c.TryCast<EntityConditionCheckProgressKey>();
            if (pk != null)
            {
                bool has = false;
                try { has = player?.GetComponent<ProgressionComponent>()?.HasProgressionKey(pk.progressionKeyToCheck) == true; } catch { }
                return $"clé {pk.progressionKeyToCheck?.name ?? "?"} requise={pk.shouldHave} actuelle={has}";
            }
            // Conditions composées : sous-conditions trouvées par réflexion
            var real = Real(c);
            var subs = new List<string>();
            foreach (var p in real.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                object v;
                try { v = p.GetValue(real); } catch { continue; }
                if (v is EntityConditionAbstract sc) subs.Add(Describe(sc, player, depth + 1));
                else if (v is Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<EntityConditionAbstract> arr)
                    foreach (var x in arr) subs.Add(Describe(x, player, depth + 1));
                else if (v is Il2CppSystem.Collections.Generic.List<EntityConditionAbstract> list)
                    for (int i = 0; i < list.Count; i++) subs.Add(Describe(list[i], player, depth + 1));
            }
            string ok = "?";
            try { ok = player != null ? c.IsConditionValidated(player).ToString() : "?"; } catch { }
            return $"{c.GetIl2CppType().Name}={ok}" + (subs.Count > 0 ? "[" + string.Join(" ; ", subs) + "]" : "");
        }
        catch (Exception ex) { return "?" + ex.Message; }
    }

    /// <summary>
    /// Diagnostic : objets proches (60 u.) qui peuvent condamner un passage selon l'histoire — portes verrouillées,
    /// évènements sur condition, portails, déclencheurs à clé — avec leur condition et leur état.
    /// </summary>
    public static void DumpObstacles(string when)
    {
        try
        {
            MotherBase.EntityFramework.Entity player = null;
            try { MainCharacterSingleton.TryGetEntity(out player); } catch { }
            var pos = GameBridge.PlayerPosition();
            var sb = new StringBuilder();
            void Each<T>(Func<T, string> line) where T : Component
            {
                foreach (var o in Resources.FindObjectsOfTypeAll(Il2CppType.Of<T>()))
                {
                    var x = o.TryCast<T>();
                    if (x == null || !x.gameObject.scene.IsValid()) continue;
                    float d = Vector3.Distance(x.transform.position, pos);
                    if (d > 60f) continue;
                    string s;
                    try { s = line(x); } catch (Exception ex) { s = "? " + ex.Message; }
                    if (s != null) sb.AppendLine($"  {typeof(T).Name} '{x.name}' à {d:0} actif={x.gameObject.activeInHierarchy} {s}");
                }
            }
            Each<LockedDoorComponent>(x => "condition=" + Describe(x.changeStateCondition, player));
            Each<EntityConditionBasedEvent>(x => "ouverture=" + Describe(x.openCondition, player));
            Each<EntityConditionUnityEvent>(x => "condition=" + Describe(x.condition, player));
            Each<GPE_Gate>(x => $"ouvert={x.isOpen}");
            Each<TriggerWithMultipleProgressionKeyChecks>(x => "clés=[" + string.Join(",", ToNames(x.progressionKeys)) + "]");
            Each<Trigger>(x => x.condition == null ? null : "condition=" + Describe(x.condition, player)); // seulement les déclencheurs conditionnels
            Plugin.Logger.LogInfo($"[Boss] obstacles ({when}), joueur en {pos} :\n{sb}");
        }
        catch (Exception ex) { Plugin.Logger.LogWarning("[Boss] obstacles : " + ex.Message); }
    }

    static IEnumerable<string> ToNames(object keys)
    {
        var res = new List<string>();
        try
        {
            if (keys is System.Collections.IEnumerable e)
                foreach (var k in e) res.Add((k as UnityEngine.Object)?.name ?? Short(k));
        }
        catch { }
        return res;
    }

    /// <summary>Retire du registre des acteurs les entrées dont l'objet a été détruit (combat précédent).</summary>
    public static int PurgeDeadActors()
    {
        int n = 0;
        try
        {
            var dict = CutsceneManager.Instance?._cutsceneActors;
            if (dict == null) return 0;
            var dead = new List<CharacterData>();
            foreach (var kv in dict)
                if (kv.Value == null || kv.Value.Cast<UnityEngine.Object>() == null) dead.Add(kv.Key);
            foreach (var k in dead) { dict.Remove(k); n++; }
            if (n > 0) Plugin.Logger.LogInfo($"[Boss] {n} acteur(s) détruit(s) retiré(s) du registre des cinématiques : {string.Join(", ", dead.ConvertAll(k => k?.name ?? "null"))}");
        }
        catch (Exception ex) { Plugin.Logger.LogWarning("[Boss] registre des acteurs : " + ex.Message); }
        return n;
    }

    public static void DumpDirectors(string when)
    {
        try
        {
            var sb = new StringBuilder();
            int count = 0;
            foreach (var o in Resources.FindObjectsOfTypeAll(Il2CppType.Of<CutsceneDirector>()))
            {
                var d = o.TryCast<CutsceneDirector>();
                if (d == null || !d.gameObject.scene.IsValid()) continue;
                var data = d._resolverData;
                var nodes = data?.Nodes;
                if (nodes == null) continue;
                bool boss = false;
                foreach (var n in nodes) { string tn = n?.GetIl2CppType().Name ?? ""; if (tn.Contains("Boss")) { boss = true; break; } }
                count++;
                if (!boss)
                {
                    sb.AppendLine($"  director '{d.name}' actif={d.isActiveAndEnabled} données='{data.name}' {nodes.Length} nœuds (aucun nœud de boss)");
                    continue;
                }
                var cur = new List<int>();
                try { var cn = d._resolver?._currentNodes; if (cn != null) for (int i = 0; i < cn.Count; i++) cur.Add(cn[i].Index); } catch { }
                sb.AppendLine($"  director '{d.name}' actif={d.isActiveAndEnabled} données='{data.name}' départ={Ints(data.StartIndexes)} en cours=[{string.Join(",", cur)}]");
                DumpNodes(sb, nodes, "    ", 0);
            }
            foreach (var o in Resources.FindObjectsOfTypeAll(Il2CppType.Of<BossFight>()))
            {
                var f = o.TryCast<BossFight>();
                if (f == null || !f.gameObject.scene.IsValid()) continue;
                sb.AppendLine($"  combat '{f.name}' actif={f.isActiveAndEnabled} boss={f._bossData?.name ?? "?"} orbePrêt={Try(() => f.IsOrbReady())} orbeDisparu={Try(() => f.HasOrbDisappeared())}");
            }
            foreach (var o in Resources.FindObjectsOfTypeAll(Il2CppType.Of<BossGateComponent>()))
            {
                var g = o.TryCast<BossGateComponent>();
                if (g == null || !g.gameObject.scene.IsValid()) continue;
                sb.AppendLine($"  porte '{g.name}' active={g.isActiveAndEnabled} boss={g.linkedBossData?.name ?? "?"} état={g.currentState}");
            }
            Plugin.Logger.LogInfo($"[Boss] graphes de combat ({when}) : {count} director(s)\n" + sb);
        }
        catch (Exception ex) { Plugin.Logger.LogWarning("[Boss] graphes : " + ex); }
    }
}
