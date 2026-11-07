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
                if (t.clamp.HasValue && dmg.maxHealth > 0)
                {
                    // Plancher = part du maximum (1 = 100 % : le boss restait plein, comme le joueur avant la 0.64.3) :
                    // 1 / max = 1 PV. Écrit seulement s'il change (réécrire une stat la signale modifiée en permanence)
                    var stats = e.GetComponent<StatsStateComponent>();
                    float minRatio = 1f / dmg.maxHealth;
                    if (stats != null && Mathf.Abs(stats.GetStatObject(clampType).GetFloat(e) - minRatio) > 1e-5f)
                        stats.SetFloat(clampType, minRatio);
                }
                if (Lock)
                {
                    if (!locked.TryGetValue(id, out int hp))
                    {
                        locked[id] = hp = dmg.currentHealth;
                        Plugin.Logger.LogInfo($"[Boss] PV figés sur {e.name} : {hp} / {dmg.maxHealth}");
                    }
                    if (dmg.currentHealth != hp) { dmg.SetCurrentHealth(e, hp, true); RefreshBossBar(e, dmg); }
                    continue;
                }
                float floor = t.clamp.HasValue ? 1.5f : dmg.maxHealth * FallbackRefill;
                if (dmg.currentHealth <= floor) { dmg.SetCurrentHealth(e, dmg.maxHealth, true); RefreshBossBar(e, dmg); }
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning($"[Boss] PV des boss désactivés suite à une erreur : {ex.Message}");
            Infinite = Lock = false;
        }
    }

    /// <summary>
    /// Barre de vie du boss (HUDControllerHealth en contexte Boss) mise à jour avec les PV du boss ; la barre du joueur
    /// n'est jamais touchée ici (et Cheats.RefreshBars ne touche que celle du joueur).
    /// </summary>
    static void RefreshBossBar(Entity e, Damageable dmg)
    {
        try
        {
            var init = new EntityInitHealthEvent { e = e, damageable = dmg };
            foreach (var o in UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<HUDControllerHealth>()))
            {
                var hud = o.Cast<HUDControllerHealth>();
                if (hud.hudControllerHealthContext == HUDControllerHealthContext.Boss) hud.ProcessInitHealthEvent(init);
            }
        }
        catch (Exception ex) { Plugin.Logger.LogWarning("[Boss] barre de vie : " + ex.Message); }
    }

    /// <summary>Boss présents : entités qui portent un BossComponent (sur elles ou un parent), jamais le joueur.</summary>
    static void Scan()
    {
        bosses.Clear();
        MainCharacterSingleton.TryGetEntity(out var player);
        foreach (var o in UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<BossComponent>()))
        {
            var bc = o.TryCast<BossComponent>();
            if (bc == null) continue;
            var e = bc.GetComponent<Entity>() ?? bc.GetComponentInParent<Entity>();
            if (e != null && e != player && !bosses.Contains(e)) bosses.Add(e);
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
