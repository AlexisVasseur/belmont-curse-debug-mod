using System;
using System.Collections.Generic;
using MotherBase.ApplicationFlowManager;
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
            if (gsm != null && !gsm.IsGameplayState()) PopLeakedStates(gsm);
            if (gsm != null && !gsm.IsGameplayState())
                return $"non-gameplay state (id {gsm.GetActiveStateId()}, {gsm.activeState?.name})";
        }
        catch (Exception ex) { Plugin.Logger.LogWarning("[État] " + ex.Message); }
        return null;
    }

    /// <summary>
    /// Dépile les états de jeu orphelins (dialogue, séquence…) : poussés par un objet de la salle détruit par
    /// un rechargement rapide, ils ne sont plus jamais retirés et survivent même à un rechargement complet.
    /// On garde l'état de chargement et le menu pause s'ils sont réellement en cours.
    /// </summary>
    static void PopLeakedStates(GameStateManager gsm)
    {
        for (int n = 0; n < 8 && !gsm.IsGameplayState(); n++)
        {
            var states = gsm._states;
            if (states == null || states.Count <= 1) return;
            var top = states.Peek();
            var st = top.Item2;
            if (st == null) return;
            // Seulement les états qu'un objet de salle peut laisser derrière lui ; jamais les menus
            // (dépiler FullscreenMenuState avec le menu ouvert bloquait le jeu)
            string sn = st.name;
            bool leakable = sn == gsm.dialogueState?.name || sn == gsm.sequenceState?.name
                || sn == gsm.timelineLockInputState?.name || sn == gsm.recoveryState?.name;
            if (!leakable)
            {
                Plugin.Logger.LogInfo($"[État] état '{sn}' (id {top.Item1}) laissé en place");
                return;
            }
            Plugin.Logger.LogInfo($"[État] état orphelin dépilé : '{st.name}' (id {top.Item1})");
            LoadSequence.Trace($"état orphelin dépilé : '{st.name}' (id {top.Item1})");
            gsm.PopState(top.Item1, gsm);
            if (states.Count > 0 && states.Peek().Item1 == top.Item1) return; // refusé par le jeu
        }
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
