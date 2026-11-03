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
        // Cinématique interrompue : son scénario (dans la salle) doit être détruit, sinon il continue après le chargement
        if (interrupted != null) { var why = interrupted; interrupted = null; return why; }
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

    static string interrupted;

    // États qu'une cinématique ou une fin de combat pose puis retire elle-même (absorption de l'orbe, dialogue…)
    static readonly HashSet<string> SequenceStates = new()
    {
        "GameplayAbsorptionState", "DialogueState", "SequenceState", "TimelineLockInputState", "CinematicState", "RecoveryState",
    };

    /// <summary>États de jeu empilés, du plus récent au plus ancien (trace des chargements).</summary>
    public static string StateStack()
    {
        try
        {
            var states = GameStateManager.GetInstance()?._states;
            if (states == null) return "?";
            var names = new List<string>();
            foreach (var t in states.ToArray()) names.Add($"{t?.Item2?.name}#{t?.Item1}");
            return string.Join(", ", names);
        }
        catch (Exception ex) { return "? " + ex.Message; }
    }

    /// <summary>
    /// Chargement pendant une cinématique ou une fin de combat (absorption après Medusa…) : le scénario qui devait retirer
    /// ses états est détruit par le chargement, ils restaient pour toujours (joueur bloqué). On les retire avant.
    /// </summary>
    public static void ClearSequenceStates()
    {
        interrupted = null;
        try
        {
            var gsm = GameStateManager.GetInstance();
            var states = gsm?._states;
            if (states == null) return;
            foreach (var t in states.ToArray())
            {
                var st = t?.Item2;
                if (st == null || !SequenceStates.Contains(st.name)) continue;
                gsm.PopState(t.Item1, gsm);
                bool still = false;
                foreach (var u in gsm._states.ToArray()) if (u?.Item1 == t.Item1) still = true;
                interrupted = $"cutscene interrupted ({st.name})";
                string msg = $"état de cinématique '{st.name}' (id {t.Item1}) {(still ? "refusé par le jeu" : "retiré avant le chargement")}";
                Plugin.Logger.LogInfo("[État] " + msg);
                LoadSequence.Trace(msg);
            }
        }
        catch (Exception ex) { Plugin.Logger.LogWarning("[État] états de cinématique : " + ex.Message); }
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
