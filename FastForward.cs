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
    static float lastCutscene;

    public static bool CutscenePlaying()
    {
        try
        {
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
            else if (playing) { armed = true; lastCutscene = now; notify?.Invoke($"Cutscenes: fast-forward ×{Speed:0}", false); }
            else notify?.Invoke("No cutscene to fast-forward", false);
        }
        if (!armed) return;

        if (playing) lastCutscene = now;
        else if (now - lastCutscene > DisarmAfter) { Disarm(); return; }

        // Pause du jeu (timeScale 0) : on n'y touche pas
        if (playing && Time.timeScale > 0f)
        {
            if (Time.timeScale != Speed) Time.timeScale = Speed;
            speeding = true;
        }
        else if (!playing && speeding)
        {
            if (Time.timeScale == Speed) Time.timeScale = 1f;
            speeding = false;
        }
    }

    static void Disarm()
    {
        if (speeding && Time.timeScale == Speed) Time.timeScale = 1f;
        speeding = false;
        armed = false;
    }
}
