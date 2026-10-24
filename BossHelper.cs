using System;
using System.Collections.Generic;
using MotherBase.EntityFramework;
using UnityEngine;

namespace BelmontPractice;

/// <summary>
/// États globaux qui survivent à un rechargement de salle (combat de boss, cinématique…).
/// Un rechargement rapide détruit la salle sans les dépiler : le boss ne peut alors plus se déclencher.
/// </summary>
public static class BossHelper
{
    /// <summary>Raison pour laquelle un rechargement rapide n'est pas sûr (null = sûr).</summary>
    public static string UnsafeForFastLoad()
    {
        try
        {
            var boss = BossManager.Get()?.currentBossVariable?.CurrentValue;
            if (boss != null) return $"boss fight in progress ({boss.name})";
        }
        catch (Exception ex) { Plugin.Logger.LogWarning("[Boss] " + ex.Message); }

        try
        {
            var gsm = GameStateManager.GetInstance();
            if (gsm != null && !gsm.IsGameplayState())
                return $"non-gameplay state (id {gsm.GetActiveStateId()})";
        }
        catch (Exception ex) { Plugin.Logger.LogWarning("[État] " + ex.Message); }
        return null;
    }

    static IEnumerable<BossFightTrigger> TriggersInRoom(List<Transform> roots)
    {
        foreach (var root in roots)
            foreach (var t in root.GetComponentsInChildren<BossFightTrigger>(true))
                yield return t;
    }

    /// <summary>
    /// Après un rechargement : réarme les déclencheurs de boss non vaincus, pour que le combat démarre
    /// quand le joueur est (ou entre) dans l'arène, même si la savestate a été prise après le déclenchement.
    /// </summary>
    public static void RearmTriggers(List<Transform> roots, Vector3 playerPos)
    {
        foreach (var bft in TriggersInRoom(roots))
        {
            try
            {
                bool defeated = bft.IsBossDefeated();
                var trig = bft._trigger;
                bool inside = trig != null && trig.ContainsPoint(playerPos);
                string info = $"'{bft.name}' vaincu={defeated} déclenché={bft.encounterHasBeenTriggered} rencontres={bft.encounterNumber} joueurDedans={inside}";
                if (defeated || bft.encounterHasBeenTriggered || trig == null)
                {
                    LoadSequence.Trace("[Boss] " + info + " → rien à faire");
                    continue;
                }
                trig.entitiesInside?.Clear(); // le joueur sera vu comme « entrant »
                trig.EnableTrigger();
                LoadSequence.Trace("[Boss] " + info + " → déclencheur réarmé");
                Plugin.Logger.LogInfo("[Boss] déclencheur réarmé : " + info);
            }
            catch (Exception ex) { LoadSequence.Trace("[Boss] erreur : " + ex.Message); }
        }
    }
}
