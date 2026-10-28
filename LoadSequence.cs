using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace BelmontPractice;

/// <summary>
/// Déroulé d'un chargement de savestate, entièrement derrière un écran noir :
///   FadeIn    jeu en pause, l'écran passe au noir
///   Loading   écran noir, jeu en marche : restauration + rechargement de salle / niveau
///   Waiting   écran noir : on attend que le jeu ait TOUT terminé (chargements, transitions, caméra)
///   Settling  écran noir : remise en place finale, quelques frames pour que la caméra se recale
///   FadeOut   jeu en pause, l'image revient sur une scène figée et en place
/// </summary>
public class LoadSequence
{
    enum Phase { Idle, FadeIn, Loading, Waiting, Settling, FadeOut }

    Phase phase = Phase.Idle;
    SaveState state;
    bool fastLoad;
    float alpha;
    float phaseStart, sequenceStart, blackSince, idleSince;
    int phaseFrames;
    bool sawBusy;
    HashSet<string> baseline = new();
    string lastReasons = "";

    public bool Active => phase != Phase.Idle;
    public Action<string, bool> Notify;

    static StringBuilder trace;

    /// <summary>Ajoute une ligne à la trace du chargement en cours (fichier écrit à la fin).</summary>
    public static void Trace(string line)
    {
        if (trace == null || trace.Length > 200_000) return;
        trace.Append($"{Time.unscaledTime:0.000}  f{Time.frameCount}  ").AppendLine(line);
    }

    public void Start(SaveState s)
    {
        if (Active) return;
        state = s;
        trace = new StringBuilder();
        sequenceStart = Time.unscaledTime;
        // Raisons « occupé » déjà présentes avant le chargement : considérées comme normales.
        baseline = new HashSet<string>(GameBridge.BusyReasons());
        Trace($"début '{s.Name}' niveau={s.Level} salle={s.Room} pos={s.Position} baseline=[{string.Join(",", baseline)}]");

        if (Plugin.FadeToBlack.Value) { Pause(); Enter(Phase.FadeIn); }
        else Enter(Phase.Loading);
    }

    void Enter(Phase p)
    {
        Trace($"→ {p} (alpha={alpha:0.00}, timeScale={Time.timeScale})");
        phase = p;
        phaseStart = Time.unscaledTime;
        phaseFrames = 0;
    }

    public void Update()
    {
        if (phase == Phase.Idle) return;
        phaseFrames++;
        float dt = Time.unscaledDeltaTime;
        float inPhase = Time.unscaledTime - phaseStart;

        switch (phase)
        {
            case Phase.FadeIn:
                // On ne touche à rien avant qu'une frame entièrement noire ait été affichée.
                if (alpha >= 1f) { blackSince = Time.unscaledTime; Enter(Phase.Loading); break; }
                alpha = Mathf.Min(1f, alpha + dt / Mathf.Max(0.01f, Plugin.FadeInDuration.Value));
                break;

            case Phase.Loading:
                Resume();
                GameBridge.SpeedUpRoomTransitions();
                try
                {
                    fastLoad = GameBridge.Restore(state);
                    Notify?.Invoke($"Loaded: {state.Name}", false);
                }
                catch (Exception ex)
                {
                    Notify?.Invoke("Load failed: " + ex.Message, true);
                    Plugin.Logger.LogError(ex);
                    Trace("ERREUR " + ex);
                    Finish();
                    break;
                }
                sawBusy = false;
                idleSince = -1f;
                Enter(Phase.Waiting);
                break;

            case Phase.Waiting:
            {
                var reasons = GameBridge.BusyReasons();
                reasons.RemoveAll(baseline.Contains);
                // La salle est rechargée, il ne reste que la caméra qui glisse : on la pose directement.
                if (reasons.Count == 1 && reasons[0] == "camera_transition")
                {
                    GameBridge.CutCameraBlend();
                    GameBridge.SnapCameras();
                    sawBusy = true;
                    reasons.Clear();
                }
                string joined = string.Join(",", reasons);
                if (joined != lastReasons)
                {
                    Trace($"occupé=[{joined}] pos={GameBridge.PlayerPosition()}");
                    lastReasons = joined;
                }

                if (reasons.Count > 0) { sawBusy = true; idleSince = -1f; }
                else if (idleSince < 0f) idleSince = Time.unscaledTime;

                float idleFor = idleSince < 0f ? 0f : Time.unscaledTime - idleSince;
                // Une activité peut démarrer quelques frames après la demande : sans l'avoir vue, on attend 1 s.
                bool done = idleFor >= Plugin.SettleDuration.Value && (sawBusy || inPhase >= 1f);
                float timeout = fastLoad ? 6f : 90f;
                if (!done && inPhase < timeout) break;
                if (!done) Trace($"DÉLAI DÉPASSÉ, toujours occupé=[{joined}]");

                RestoreAfterLoad();
                Enter(Phase.Settling);
                break;
            }

            case Phase.Settling:
            {
                // Laisse la caméra rattraper la position finale, toujours derrière le noir.
                GameBridge.CutCameraBlend();
                bool cameraDone = !GameBridge.CameraBlending() || inPhase > 1f;
                bool holdDone = Time.unscaledTime - blackSince >= Plugin.BlackHoldDuration.Value;
                if (phaseFrames < 4 || !cameraDone || !holdDone) break;

                // Dernière correction (quelques frames ont tourné), puis on fige tout.
                FinalTouch();
                if (Plugin.FadeToBlack.Value) { Pause(); Enter(Phase.FadeOut); }
                else Finish();
                break;
            }

            case Phase.FadeOut:
                alpha = Mathf.Max(0f, alpha - dt / Mathf.Max(0.01f, Plugin.FadeOutDuration.Value));
                if (alpha <= 0f) Finish();
                break;
        }

        // Le Canvas est rendu en fin de frame : l'opacité fixée ici est celle affichée à cette frame.
        FadeOverlay.Set(alpha);
    }

    /// <summary>Le jeu a fini : on remet tout ce que le chargement du jeu a pu écraser.</summary>
    void RestoreAfterLoad()
    {
        Trace($"finalisation, joueur en {GameBridge.PlayerPosition()}");
        GameBridge.RestoreRoomTransitions();
        try
        {
            GameBridge.SnapToGround(state); // téléportation : posé sur le sol s'il est juste en dessous
            GameBridge.FixPositionAfterRoomLoad(state);
            GameBridge.RestoreRoomObjects(state);
            GameBridge.ApplyLiveStats(state);
            GameBridge.SnapCameras();
            GameBridge.RearmBossTriggers();
        }
        catch (Exception ex) { Plugin.Logger.LogWarning("[Finalisation] " + ex.Message); Trace("ERREUR finalisation " + ex.Message); }
    }

    void FinalTouch()
    {
        try
        {
            GameBridge.PlacePlayer(state);
            GameBridge.RestoreRoomObjects(state);
            GameBridge.ApplyLiveStats(state);
        }
        catch (Exception ex) { Trace("ERREUR dernière correction " + ex.Message); }
        Trace($"image prête, joueur en {GameBridge.PlayerPosition()}, occupé=[{string.Join(",", GameBridge.BusyReasons())}]");
    }

    void Finish()
    {
        Resume();
        GameBridge.RestoreRoomTransitions();
        alpha = 0f;
        FadeOverlay.Set(0f);
        Cheats.ResyncNoclip();
        // Position imposée pour le rechargement complet : elle ne doit pas resservir à la réapparition après une mort
        try
        {
            var spawn = MainCharacterSpawnLogic.Get();
            if (spawn != null && spawn._forceSpawnPosition) { spawn.ClearForcePosition(); Trace("position imposée effacée"); }
        }
        catch (Exception ex) { Trace("ERREUR effacement position imposée " + ex.Message); }
        SaveGuard.ReleaseSoon();
        Bosses.LogTriggers("après chargement");
        Enter(Phase.Idle);
        Trace($"terminé en {Time.unscaledTime - sequenceStart:0.00}s");
        Plugin.Logger.LogInfo($"[Chargement] terminé en {Time.unscaledTime - sequenceStart:0.00}s");
        try { File.WriteAllText(Path.Combine(SlotStore.Folder, "..", "trace_dernier_chargement.txt"), trace.ToString()); }
        catch { }
        trace = null;
        lastReasons = "";
    }

    // ---------- Pause ----------

    bool paused;
    float savedTimeScale = 1f;

    void Pause()
    {
        if (!Plugin.PauseDuringFade.Value || paused) return;
        savedTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
        Time.timeScale = 0f;
        paused = true;
    }

    void Resume()
    {
        if (!paused) return;
        Time.timeScale = savedTimeScale;
        paused = false;
    }

}
