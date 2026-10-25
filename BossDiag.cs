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
    static object Real(Il2CppObjectBase o)
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
                            if (p.GetIndexParameters().Length > 0 || p.Name == "ElseNodes" || p.Name == "IsUniquePerBranch") continue;
                            try { fields.Add(p.Name + "=" + Short(p.GetValue(real))); } catch { }
                        }
                    string els = "";
                    try { if (real.GetType().GetProperty("ElseNodes")?.GetValue(real) is Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<int> e) els = " sinon→" + Ints(e); } catch { }
                    sb.AppendLine($"    [{i}] {t.Name.Replace("NodeData", "")} {string.Join(" ", fields)} →{Ints(n.NextNodes)}{els}");
                }
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
