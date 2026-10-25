using System;
using System.Collections.Generic;
using UnityEngine;

namespace BelmontPractice;

/// <summary>
/// Cinématiques en accéléré (raccourci, F7 / Y à la manette par défaut), pour la cinématique en cours et celles qui
/// suivent (fin de combat, absorption, suite de l'histoire…) :
///  - jouées avec les personnages du jeu (scénario, dialogues) : accélérées (×10 par défaut, réglable), lignes de
///    dialogue terminées et passages sautables sautés ;
///  - vidéos (hors moteur du jeu) : jouées en entier à vitesse normale, ainsi que juste après. Ni accélérées ni passées :
///    passées (même avec la touche du jeu), elles laissent l'écran noir après la scène de Trevor (Death I).
/// Le gameplay n'est jamais accéléré. S'arrête à une nouvelle pression, à un chargement, ou après 20 s sans cinématique.
/// </summary>
public static class FastForward
{
    const float DisarmAfter = 20f, AfterVideo = 1.5f;

    // États de jeu posés pendant une cinématique (pas le verrou des menus, qui peut rester en plein gameplay)
    static readonly HashSet<string> CutsceneStates = new()
    {
        "DialogueState", "GameplayAbsorptionState", "SequenceState", "TimelineLockInputState", "CinematicState",
    };

    static bool armed;
    public static bool Armed => armed;
    static float lastCutscene, lastVideo = -100f, appliedSpeed;

    public static float Speed => Mathf.Clamp(Plugin.CutsceneSpeed?.Value ?? 10f, 2f, 20f);

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
                Plugin.Logger.LogInfo($"[Cinématiques] accélération ×{Speed:0} : cinématique={CutsceneUIManager.Instance?.IsPlayingCutscene}, scénarios={CutsceneManager.Instance?._directors?.Count}");
            }
            else notify?.Invoke("No cutscene to fast-forward", false);
        }
        if (!armed) return;

        // Étapes qui attendent le joueur (absorption de l'orbe, fenêtre de la carte d'arcane) : faites à sa place
        bool waiting = AutoAdvance();
        if (waiting) playing = true;

        if (playing) lastCutscene = now;
        else if (now - lastCutscene > DisarmAfter) { Disarm(); return; }
        if (!playing) { RestoreSpeed(); return; }

        // Vidéo : laissée telle quelle, à vitesse normale
        if (InVideo) { RestoreSpeed(); return; }

        // Cinématique jouée avec les personnages du jeu : temps du jeu accéléré (pause du jeu, timeScale 0 : on n'y touche pas)
        float speed = Speed;
        if (Time.timeScale > 0f && Time.timeScale != speed)
        {
            Time.timeScale = speed;
            appliedSpeed = speed;
        }
        // Les scénarios avancent sur leur propre horloge : passes supplémentaires à chaque image
        try
        {
            var cm = CutsceneManager.Instance;
            float dt = Time.unscaledDeltaTime;
            if (cm != null) for (int i = 1; i < speed; i++) cm.UpdateDirectors(dt);
        }
        catch (Exception ex) { Plugin.Logger.LogWarning("[Cinématiques] scénarios : " + ex.Message); }
        // Ce que fait la touche du joueur : lignes de dialogue terminées, passages sautables sautés
        SkipDialogueLines();
        try { SkipSkippableSegments(); }
        catch (Exception ex) { Plugin.Logger.LogWarning("[Cinématiques] passages sautables : " + ex.Message); }
    }

    /// <summary>Vidéo en cours, ou terminée depuis moins de 1,5 s (le scénario refait alors son fondu).</summary>
    public static bool InVideo
    {
        get
        {
            float now = Time.unscaledTime;
            try { if (VideoPlaying()) lastVideo = now; } catch { }
            return now - lastVideo < AfterVideo;
        }
    }

    static bool VideoPlaying()
    {
        if (HasState("CinematicState")) return true;
        var vc = CutsceneUIManager.Instance?._videoController;
        return vc != null && (vc.IsPlayingVideo || vc.IsPreparingVideo);
    }

    static void RestoreSpeed()
    {
        if (appliedSpeed > 0f && Time.timeScale == appliedSpeed) Time.timeScale = 1f;
        appliedSpeed = 0f;
    }

    static float acquisitionSince = -1f;

    static bool HasState(string name)
    {
        try
        {
            var states = GameStateManager.GetInstance()?._states;
            if (states == null) return false;
            foreach (var t in states.ToArray()) if (t?.Item2?.name == name) return true;
        }
        catch { }
        return false;
    }

    /// <summary>
    /// Fait à la place du joueur ce que la fin de combat attend de lui. Retourne true si une telle étape est en cours.
    ///  - Absorption (GameplayAbsorptionState) : l'interaction longue (RB maintenu) est lancée sur le personnage.
    ///  - Fenêtres d'acquisition (carte d'arcane, objet…) : fermées après un court instant d'affichage.
    /// </summary>
    static bool AutoAdvance()
    {
        bool waiting = false;
        if (HasState("GameplayAbsorptionState"))
        {
            waiting = true;
            try { Absorb(); }
            catch (Exception ex) { Plugin.Logger.LogWarning("[Cinématiques] absorption : " + ex.Message); }
        }
        else EndAbsorb();

        if (HasState("FullscreenMenuState") && CloseAcquisitionWindows(dryRun: true))
        {
            waiting = true;
            if (acquisitionSince < 0f) acquisitionSince = Time.unscaledTime;
            else if (Time.unscaledTime - acquisitionSince > 0.4f) { CloseAcquisitionWindows(dryRun: false); acquisitionSince = -1f; }
        }
        else acquisitionSince = -1f;
        return waiting;
    }

    // Absorption en cours : début du maintien simulé de RB, repli déjà fait, états de touche tenus
    static float absorbStartedAt = -1f;
    static bool absorbFallbackDone;
    static MotherBase.ActionFramework.PlayerInput heldInput;
    static readonly List<int> heldStates = new();

    /// <summary>
    /// L'orbe s'absorbe en maintenant RB pendant l'action d'absorption du personnage. On tient RB à la place du joueur,
    /// dans l'état des touches que lit le jeu : il fait alors toute la suite (animation, orbe absorbée, évènement
    /// attendu par le scénario). Repli au bout de 4 s si le scénario attend toujours : évènement déclenché et orbe désactivée.
    /// </summary>
    static void Absorb()
    {
        var pending = PendingEvents();
        if (pending.Count == 0) { ReleaseHeld(); return; } // orbe pas encore prête, ou absorption faite
        float now = Time.unscaledTime;
        bool first = absorbStartedAt < 0f;
        if (first)
        {
            absorbStartedAt = now;
            StartAbsorption();
        }
        HoldStates(first);
        if (absorbFallbackDone || now - absorbStartedAt < 1.5f) return;
        absorbFallbackDone = true;
        ReleaseHeld();
        foreach (var ev in pending) ev.OnEventTriggered();
        foreach (var it in OrbInteractibles()) it.DisableInteractible();
        Plugin.Logger.LogWarning("[Cinématiques] absorption : RB simulé sans effet, évènement déclenché et orbe désactivée (repli)");
    }

    static void StartAbsorption()
    {
        if (!MainCharacterSingleton.TryGetEntity(out MotherBase.EntityFramework.Entity player) || player == null) return;
        var actor = player.GetComponentInChildren<MotherBase.ActionFramework.Actor>(true);
        var input = player.GetComponentInChildren<MotherBase.ActionFramework.PlayerInput>(true);
        if (actor == null || input?.data == null) { Plugin.Logger.LogWarning("[Cinématiques] absorption : personnage sans Actor / PlayerInput"); return; }
        Plugin.Logger.LogInfo($"[Cinématiques] absorption : actions en cours [{ActiveActionNames(actor)}]");
        foreach (var it in OrbInteractibles())
            Plugin.Logger.LogInfo($"[Cinématiques] absorption : orbe '{it.gameObject.name}', personnage → '{it.interactorInteractionAction?.name}', orbe → '{it.selfInteractionAction?.name}', actif={it.enabled}");

        // États de touche tenus (lus par les actions) liés à RB
        var states = input.data.inputStates;
        for (int i = 0; states != null && i < states.Length; i++)
        {
            var b = states[i];
            string paths = BindingPaths(b?.input?.action);
            bool rb = paths.IndexOf("rightShoulder", StringComparison.OrdinalIgnoreCase) >= 0;
            if (rb) heldStates.Add(i);
            if (rb || Plugin.BossDiagnostics.Value) Plugin.Logger.LogInfo($"[Cinématiques] absorption : état {i} '{b?.state?.name}' ← '{b?.input?.action?.name}' [{paths}]{(rb ? " tenu" : "")}");
        }
        heldInput = input;

        // Évènements de touche (début d'appui) liés à RB : envoyés comme à l'appui réel
        var binds = input.data.inputBindings;
        for (int i = 0; binds != null && i < binds.Length; i++)
        {
            var b = binds[i];
            if (b == null || b.inputPhase == UnityEngine.InputSystem.InputActionPhase.Canceled) continue;
            string paths = BindingPaths(b.input?.action);
            if (paths.IndexOf("rightShoulder", StringComparison.OrdinalIgnoreCase) < 0) continue;
            bool ok = false;
            try
            {
                if (b.signal != null) ok = actor.TryProcessSignal(player, b.signal);
                else if (b.action != null) ok = actor.TryStartAction(player, b.action);
            }
            catch (Exception ex) { Plugin.Logger.LogWarning("[Cinématiques] absorption : " + ex.Message); }
            Plugin.Logger.LogInfo($"[Cinématiques] absorption : appui '{b.input?.action?.name}' ({b.inputPhase}) → signal '{b.signal?.name}' / action '{b.action?.name}' : {(ok ? "accepté" : "refusé")}");
        }
        Plugin.Logger.LogInfo($"[Cinématiques] absorption : RB tenu ({heldStates.Count} état(s) sur {input.inputActionStates?.Length}), actions [{ActiveActionNames(actor)}]");
    }

    static void HoldStates(bool first)
    {
        var arr = heldInput?.inputActionStates;
        if (arr == null) return;
        foreach (int i in heldStates)
            if (i < arr.Length) arr[i] = new MotherBase.ActionFramework.PlayerInput.InputState { pressed = first, released = false, isDown = true };
    }

    static void ReleaseHeld()
    {
        try
        {
            var arr = heldInput?.inputActionStates;
            if (arr != null)
                foreach (int i in heldStates)
                    if (i < arr.Length) arr[i] = new MotherBase.ActionFramework.PlayerInput.InputState { pressed = false, released = true, isDown = false };
        }
        catch { }
        heldStates.Clear();
        heldInput = null;
    }

    static void EndAbsorb()
    {
        ReleaseHeld();
        absorbStartedAt = -1f;
        absorbFallbackDone = false;
    }

    /// <summary>Chemins des touches d'une action de saisie (ex. "&lt;Gamepad&gt;/rightShoulder").</summary>
    static string BindingPaths(UnityEngine.InputSystem.InputAction action)
    {
        if (action == null) return "";
        var paths = new List<string>();
        try
        {
            var bindings = action.bindings;
            for (int i = 0; i < bindings.Count; i++) paths.Add(bindings[i].effectivePath);
        }
        catch { }
        return string.Join(", ", paths);
    }

    static string ActiveActionNames(MotherBase.ActionFramework.Actor actor)
    {
        var names = new List<string>();
        var active = actor.activeActions;
        for (int i = 0; active != null && i < active.Count; i++) names.Add(active[i]?.data?.name ?? "?");
        return string.Join(", ", names);
    }

    /// <summary>Interaction de l'orbe laissée par le boss vaincu (combats normaux et scriptés).</summary>
    static List<Interactible> OrbInteractibles()
    {
        var list = new List<Interactible>();
        void Add(MotherBase.EntityFramework.Entity orb)
        {
            if (orb == null || !orb.gameObject.activeInHierarchy) return;
            var it = orb.GetComponentInChildren<Interactible>(true);
            if (it != null && !list.Contains(it)) list.Add(it);
        }
        foreach (var o in UnityEngine.Object.FindObjectsOfType(Il2CppInterop.Runtime.Il2CppType.Of<BossFight>()))
            Add(o.TryCast<BossFight>()?._bossLootOrbEntity);
        foreach (var o in UnityEngine.Object.FindObjectsOfType(Il2CppInterop.Runtime.Il2CppType.Of<FakeBossFight>()))
            Add(o.TryCast<FakeBossFight>()?._bossLootOrbEntity);
        return list;
    }

    /// <summary>Étapes de scénario en cours (sous-scénarios compris) qui attendent un évènement.</summary>
    static List<ResolverWaitForCutsceneEventNode> PendingEvents()
    {
        var list = new List<ResolverWaitForCutsceneEventNode>();
        void Walk(ResolverNodeBase n, int depth)
        {
            if (n == null || depth > 4) return;
            var sub = n.TryCast<ResolverPlaySubResolverNode>();
            if (sub != null)
            {
                var cur = sub._resolver?._currentNodes;
                if (cur != null) for (int i = 0; i < cur.Count; i++) Walk(cur[i], depth + 1);
                return;
            }
            var ev = n.TryCast<ResolverWaitForCutsceneEventNode>();
            if (ev != null && !ev._isEventTriggered) list.Add(ev);
        }
        foreach (var o in UnityEngine.Object.FindObjectsOfType(Il2CppInterop.Runtime.Il2CppType.Of<CutsceneDirector>()))
        {
            var cur = o.TryCast<CutsceneDirector>()?._resolver?._currentNodes;
            if (cur != null) for (int i = 0; i < cur.Count; i++) Walk(cur[i], 0);
        }
        return list;
    }

    /// <summary>Fenêtres d'acquisition ouvertes (carte d'arcane, objet, objectif…) ; dryRun : seulement les compter.</summary>
    static bool CloseAcquisitionWindows(bool dryRun)
    {
        bool any = false;
        void Each<T>(Action<T> close) where T : Component
        {
            foreach (var o in UnityEngine.Object.FindObjectsOfType(Il2CppInterop.Runtime.Il2CppType.Of<T>()))
            {
                var c = o.TryCast<T>();
                if (c == null || !c.gameObject.activeInHierarchy) continue;
                any = true;
                if (dryRun) continue;
                try { close(c); Plugin.Logger.LogInfo($"[Cinématiques] fenêtre {typeof(T).Name} fermée"); }
                catch (Exception ex) { Plugin.Logger.LogWarning($"[Cinématiques] fermeture {typeof(T).Name} : {ex.Message}"); }
            }
        }
        Each<ArcanaAcquisitionController>(c => c.CloseArcanaAcquisition());
        Each<ItemAcquisitionController>(c => c.CloseItemAcquisition());
        Each<CollectibleAcquisitionController>(c => c.CloseAcquisition());
        // Pas la fenêtre du deck d'objectif (après Death I) : fermée pendant son animation d'ouverture, elle laisse un écran noir
        return any;
    }

    /// <summary>
    /// Passages que le jeu laisse sauter (touche A) : piste InputSkipTrack des timelines. En cours de lecture,
    /// on saute à la fin du passage, comme la touche du jeu.
    /// </summary>
    static void SkipSkippableSegments()
    {
        foreach (var o in UnityEngine.Object.FindObjectsOfType(Il2CppInterop.Runtime.Il2CppType.Of<UnityEngine.Playables.PlayableDirector>()))
        {
            var d = o.TryCast<UnityEngine.Playables.PlayableDirector>();
            if (d == null || d.state != UnityEngine.Playables.PlayState.Playing) continue;
            var tl = d.playableAsset?.TryCast<UnityEngine.Timeline.TimelineAsset>();
            if (tl == null) continue;
            double t = d.time;
            for (int i = 0; i < tl.outputTrackCount; i++)
            {
                var track = tl.GetOutputTrack(i);
                if (track?.TryCast<InputSkipTrack>() == null) continue;
                var clips = track.clips;
                for (int c = 0; clips != null && c < clips.Length; c++)
                {
                    var clip = clips[c];
                    if (clip == null || t < clip.start || t >= clip.end) continue;
                    d.time = clip.end;
                    Plugin.Logger.LogInfo($"[Cinématiques] passage sauté dans '{tl.name}' : {t:0.0} s → {clip.end:0.0} s");
                    t = clip.end;
                }
            }
        }
    }

    static float nextLineSkip;

    /// <summary>Lignes de dialogue affichées (boîte plein écran, bulles) : terminées comme par la touche du joueur.</summary>
    static void SkipDialogueLines()
    {
        // Un léger délai entre deux lignes, le temps que la suivante commence
        if (Time.unscaledTime < nextLineSkip) return;
        nextLineSkip = Time.unscaledTime + 0.1f;
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
        RestoreSpeed();
        armed = false;
        EndAbsorb();
    }
}
