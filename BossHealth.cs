using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using MotherBase.ActionFramework;
using MotherBase.EntityFramework;
using UnityEngine;

namespace BelmontPractice;

/// <summary>
/// PV des boss (onglet Bosses), plancher de PV à 1 comme pour le joueur dans les deux cas :
/// - Infinite : les PV baissent normalement (les phases changent) et la barre est remplie quand elle touche le fond ;
/// - Lock : les PV restent figés à leur valeur au moment où l'option s'applique au boss (prioritaire sur Infinite).
/// </summary>
public static class BossHealth
{
    public static bool Infinite, Lock;

    // Boss suivis : plancher de PV d'origine (null : le boss n'a pas cette stat), pour le remettre à l'arrêt
    static readonly Dictionary<int, (Entity e, float? clamp)> tracked = new();
    // PV figés de chaque boss (Lock), relevés la première fois que le verrou s'applique à lui
    static readonly Dictionary<int, int> locked = new();
    static readonly List<Entity> bosses = new();
    static float nextScan;

    /// <summary>Sans plancher de PV : remplissage dès que les PV passent sous cette part du maximum (un coup ne tue pas).</summary>
    const float FallbackRefill = 0.3f;

    /// <summary>Appelé à chaque frame (Update), en jeu.</summary>
    public static void Update()
    {
        if (!Lock) locked.Clear();
        if (!Infinite && !Lock) { if (tracked.Count > 0) Restore(); return; }
        try
        {
            if (Time.unscaledTime >= nextScan) { nextScan = Time.unscaledTime + 0.5f; Scan(); }
            var clampType = Il2CppType.Of<HealthClampMin>();
            foreach (var e in bosses)
            {
                if (e == null) continue;
                var dmg = e.GetComponent<Damageable>();
                if (dmg == null || dmg.currentHealth <= 0f) continue;
                int id = e.GetInstanceID();
                if (!tracked.TryGetValue(id, out var t))
                {
                    var stats = e.GetComponent<StatsStateComponent>();
                    float? clamp = stats != null && stats.HasStat(clampType) ? stats.GetStatObject(clampType).GetFloat(e) : null;
                    tracked[id] = t = (e, clamp);
                    Plugin.Logger.LogInfo($"[Boss] PV protégés sur {e.name} ({dmg.currentHealth:0} / {dmg.maxHealth:0}, plancher {(clamp.HasValue ? "oui" : "absent : remplissage à 30 %")})");
                }
                if (t.clamp.HasValue) e.GetComponent<StatsStateComponent>()?.SetFloat(clampType, 1f);
                if (Lock)
                {
                    if (!locked.TryGetValue(id, out int hp)) locked[id] = hp = dmg.currentHealth;
                    if (dmg.currentHealth != hp) dmg.SetCurrentHealth(e, hp, true);
                    continue;
                }
                float floor = t.clamp.HasValue ? 1.5f : dmg.maxHealth * FallbackRefill;
                if (dmg.currentHealth <= floor) dmg.SetCurrentHealth(e, dmg.maxHealth, true);
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"[Boss] PV des boss désactivés suite à une erreur : {ex.Message}");
            Infinite = Lock = false;
        }
    }

    /// <summary>Boss présents : entités qui portent un BossComponent (sur elles ou un parent).</summary>
    static void Scan()
    {
        bosses.Clear();
        foreach (var o in UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<BossComponent>()))
        {
            var bc = o.TryCast<BossComponent>();
            if (bc == null) continue;
            var e = bc.GetComponent<Entity>() ?? bc.GetComponentInParent<Entity>();
            if (e != null && !bosses.Contains(e)) bosses.Add(e);
        }
    }

    /// <summary>Arrêt : plancher de PV d'origine rendu aux boss encore présents.</summary>
    static void Restore()
    {
        var clampType = Il2CppType.Of<HealthClampMin>();
        foreach (var (e, clamp) in tracked.Values)
        {
            try { if (e != null && clamp.HasValue) e.GetComponent<StatsStateComponent>()?.SetFloat(clampType, clamp.Value); }
            catch (Exception ex) { Plugin.Logger.LogWarning("[Boss] plancher de PV : " + ex.Message); }
        }
        tracked.Clear();
        bosses.Clear();
    }
}
