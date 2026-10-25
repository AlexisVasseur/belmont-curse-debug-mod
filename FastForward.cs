using System;
using System.Collections.Generic;
using UnityEngine;

namespace BelmontPractice;

/// <summary>
/// Accélération des cinématiques (raccourci, F7 / Y à la manette par défaut). Une pression pendant une cinématique l'accélère ×10,
/// ainsi que toutes celles qui suivent (fin de combat, absorption, suite de l'histoire…). Dès que le jeu redevient
/// jouable, la vitesse normale revient aussitôt : le gameplay n'est jamais accéléré. S'arrête à une nouvelle pression,
/// à un chargement, ou après 20 s sans cinématique.
/// </summary>
public static class FastForward
{
    const float Speed = 10f, DisarmAfter = 20f;

    // États de jeu posés pendant une cinématique (pas le verrou des menus, qui peut rester en plein gameplay)
    static readonly HashSet<string> CutsceneStates = new()
    {
        "DialogueState", "GameplayAbsorptionState", "SequenceState", "TimelineLockInputState",
    };

    static bool armed, speeding;
    public static bool Armed => armed;
    static float lastCutscene;

    public static bool CutscenePlaying()
    {
        try
        {
            // Cinématique qui a pris les commandes (celles qui les laissent au joueur ne sont pas accélérées : ce serait du gameplay)
            var ui = CutsceneUIManager.Instance;
            if (ui != null && ui.IsPlayingCutscene && ui._controlCount > 0) return true;
            var states = GameStateManager.GetInstance()?._states;
            if (states == null) return false;
            foreach (var t in states.ToArray())
                if (t?.Item2 != null && CutsceneStates.Contains(t.Item2.name)) return true;
        }
        catch { }
        return false;
    }

    /// <summary>Chaque frame. pressed : raccourci pressé ; loading : chargement du mod en cours.</summary>
    public static void Update(bool pressed, bool loading, Action<string, bool> notify)
    {
        float now = Time.unscaledTime;
        if (loading) { Disarm(); return; }
        bool playing = CutscenePlaying();

        if (pressed)
        {
            if (armed) { Disarm(); notify?.Invoke("Cutscenes: normal speed", false); }
            else if (playing)
            {
                armed = true;
                lastCutscene = now;
                notify?.Invoke($"Cutscenes: fast-forward ×{Speed:0}", false);
                Plugin.Logger.LogInfo($"[Cinématiques] accélération : timeScale={Time.timeScale}, cinématique={CutsceneUIManager.Instance?.IsPlayingCutscene}, scénarios={CutsceneManager.Instance?._directors?.Count}");
            }
            else notify?.Invoke("No cutscene to fast-forward", false);
        }
        if (!armed) return;

        if (playing) lastCutscene = now;
        else if (now - lastCutscene > DisarmAfter) { Disarm(); return; }

        if (playing)
        {
            // Animations et timelines : temps du jeu accéléré (pause du jeu, timeScale 0 : on n'y touche pas)
            if (Time.timeScale > 0f)
            {
                if (Time.timeScale != Speed) Time.timeScale = Speed;
                speeding = true;
            }
            // Les scénarios avancent sur leur propre horloge : passes supplémentaires à chaque image
            try
            {
                var cm = CutsceneManager.Instance;
                float dt = Time.unscaledDeltaTime;
                if (cm != null) for (int i = 1; i < Speed; i++) cm.UpdateDirectors(dt);
            }
            catch (Exception ex) { Plugin.Logger.LogWarning("[Cinématiques] scénarios : " + ex.Message); }
            // Lignes de dialogue : comme la touche du joueur, texte affiché d'un coup puis ligne terminée (le scénario
            // passe à la suivante). Les lignes attendent sinon une touche ou leur durée.
            SkipDialogueLines();
            try
            {
                var ui = CutsceneUIManager.Instance;
                if (ui != null && ui.IsVideoPlaying()) ui.ForceStopVideo();
            }
            catch { }
        }
        else if (!playing && speeding)
        {
            if (Time.timeScale == Speed) Time.timeScale = 1f;
            speeding = false;
        }
    }

    static float nextLineSkip;

    /// <summary>Lignes de dialogue affichées (boîte plein écran, bulles) : terminées comme par la touche du joueur.</summary>
    static void SkipDialogueLines()
    {
        // Un léger délai entre deux lignes, le temps que la suivante commence
        if (Time.unscaledTime < nextLineSkip) return;
        nextLineSkip = Time.unscaledTime + 0.05f;
        foreach (var o in UnityEngine.Object.FindObjectsOfType(Il2CppInterop.Runtime.Il2CppType.Of<CutsceneLineReader>()))
        {
            try
            {
                var r = o.TryCast<CutsceneLineReader>();
                if (r == null || !r._isActive) continue;
                if (r._isReading) r.EndReading();
                r.End();
            }
            catch (Exception ex) { Plugin.Logger.LogWarning("[Cinématiques] ligne de dialogue : " + ex.Message); }
        }
    }

    static void Disarm()
    {
        if (speeding && Time.timeScale == Speed) Time.timeScale = 1f;
        speeding = false;
        armed = false;
    }
}
