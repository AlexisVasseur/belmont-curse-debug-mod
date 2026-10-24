using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Il2CppInterop.Runtime;
using RoomManager;
using UnityEngine;

namespace BelmontPractice;

/// <summary>État d'un objet mobile de la salle (scie, plateforme…) suivant un chemin.</summary>
public class MoverState
{
    public string Path { get; set; } = "";
    public float CartPosition { get; set; }
    public float CartSpeed { get; set; }
    public float CartTotal { get; set; }
    public int CartLastWaypoint { get; set; }
    public bool HasPlatform { get; set; }
    public bool PlatformForward { get; set; }
    public float PlatformDelay { get; set; }
    public bool PlatformStopped { get; set; }
    public bool PlatformWaiting { get; set; }
    public bool PlatformAtEnd { get; set; }
    public bool PlatformAtStart { get; set; }
}

/// <summary>Sauvegarde / restauration des objets mobiles de la salle courante.</summary>
public static class RoomObjects
{
    /// <summary>Objets racines de la scène de la salle (le RoomContainer et ses voisins).</summary>
    public static List<Transform> RoomRoots(RoomStreamingHandle handle)
    {
        var roots = new List<Transform>();
        var container = handle?.roomContainer;
        if (container == null) return roots;
        var scene = container.gameObject.scene;
        if (scene.IsValid())
            foreach (var go in scene.GetRootGameObjects()) roots.Add(go.transform);
        else
            roots.Add(container.transform);
        return roots;
    }

    /// <summary>Chemin stable d'un objet dans la scène (nom + index frère à chaque niveau).</summary>
    static string PathOf(Transform t)
    {
        var parts = new List<string>();
        while (t != null)
        {
            parts.Add($"{t.name}#{t.GetSiblingIndex()}");
            t = t.parent;
        }
        parts.Reverse();
        return string.Join("/", parts);
    }

    public static List<MoverState> Capture(List<Transform> roots)
    {
        var list = new List<MoverState>();
        foreach (var root in roots)
        foreach (var cart in root.GetComponentsInChildren<CinemachineDollyCartECS>(true))
        {
            var m = new MoverState
            {
                Path = PathOf(cart.transform),
                CartPosition = cart.m_Position,
                CartSpeed = cart.m_Speed,
                CartTotal = cart._totalPosition,
                CartLastWaypoint = cart._lastWaypointPosition,
            };
            var plat = cart.GetComponent<GPE_MovingPlatform>();
            if (plat != null)
            {
                m.HasPlatform = true;
                m.PlatformForward = plat.isMovingForward;
                m.PlatformDelay = plat.delayBeforeTurnBack;
                m.PlatformStopped = plat.isStopped;
                m.PlatformWaiting = plat.isWaiting;
                m.PlatformAtEnd = plat.isAtEnd;
                m.PlatformAtStart = plat.isAtStart;
            }
            list.Add(m);
        }
        return list;
    }

    public static void Restore(List<Transform> roots, List<MoverState> movers)
    {
        if (movers == null || movers.Count == 0) return;
        var byPath = new Dictionary<string, MoverState>();
        foreach (var m in movers) byPath[m.Path] = m;

        int restored = 0;
        foreach (var root in roots)
        foreach (var cart in root.GetComponentsInChildren<CinemachineDollyCartECS>(true))
        {
            if (!byPath.TryGetValue(PathOf(cart.transform), out var m)) continue;
            cart.m_Speed = m.CartSpeed;
            cart._totalPosition = m.CartTotal;
            cart._lastWaypointPosition = m.CartLastWaypoint;
            cart.SetCartPosition(m.CartPosition, out _);
            cart.m_Position = m.CartPosition;
            if (m.HasPlatform)
            {
                var plat = cart.GetComponent<GPE_MovingPlatform>();
                if (plat != null)
                {
                    plat.isMovingForward = m.PlatformForward;
                    plat.delayBeforeTurnBack = m.PlatformDelay;
                    plat.isStopped = m.PlatformStopped;
                    plat.isWaiting = m.PlatformWaiting;
                }
            }
            restored++;
        }
        Plugin.Logger.LogInfo($"[Objets] {restored}/{movers.Count} objets mobiles restaurés");
    }

    static readonly string[] Keywords = { "saw", "scie", "blade", "buzz", "rail", "trap", "piege", "spike", "gpe" };

    /// <summary>Diagnostic : liste les objets ressemblant à des pièges et leurs composants.</summary>
    public static string Dump(List<Transform> roots, string folder)
    {
        if (roots.Count == 0) throw new Exception("Salle courante introuvable");
        var sb = new StringBuilder();
        int n = 0;
        foreach (var root in roots)
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            string lower = t.name.ToLowerInvariant();
            bool match = false;
            foreach (var k in Keywords) if (lower.Contains(k)) { match = true; break; }
            if (!match && t.GetComponent<GPE_SawMovingWrapper>() == null && t.GetComponent<CinemachineDollyCartECS>() == null) continue;

            sb.Append(PathOf(t)).Append($"  pos={t.position}  actif={t.gameObject.activeInHierarchy}\n    ");
            foreach (var c in t.GetComponents<Component>())
                sb.Append(c == null ? "null" : c.GetIl2CppType().FullName).Append(", ");
            sb.AppendLine();
            if (++n >= 300) { sb.AppendLine("... (tronqué)"); break; }
        }
        sb.Insert(0, $"{n} objets, {roots.Count} racines\n");
        string path = Path.Combine(folder, $"room_dump_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
        File.WriteAllText(path, sb.ToString());
        return path;
    }
}
