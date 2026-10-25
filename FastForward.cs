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

        // Étapes qui attendent le joueur (absorption de l'orbe, fenêtre de la carte d'arcane) : faites à sa place
        bool waiting = AutoAdvance();
        if (waiting) playing = true;

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

    // Absorption en cours : moment où l'interaction a été lancée, repli déjà fait, conditions d'entrée suspendues
    static float absorbStartedAt = -1f;
    static bool absorbFallbackDone;
    static readonly List<(AStartConditionFeature f, bool stop, bool ignore)> relaxed = new();

    /// <summary>
    /// L'orbe s'absorbe en maintenant RB : l'action d'interaction longue du personnage ne dure que tant que la touche est
    /// tenue, et valide l'absorption à sa fin. On la lance nous-mêmes, sa condition « touche tenue » suspendue : le jeu
    /// fait alors toute la suite (animation, orbe absorbée, évènement attendu par le scénario).
    /// Repli au bout de 3 s si le scénario attend toujours : évènement déclenché et orbe rendue inactive.
    /// </summary>
    static void Absorb()
    {
        var pending = PendingEvents();
        if (pending.Count == 0) return; // orbe pas encore prête, ou absorption déjà faite
        float now = Time.unscaledTime;
        if (absorbStartedAt < 0f)
        {
            absorbStartedAt = now;
            StartAbsorption();
            return;
        }
        if (absorbFallbackDone || now - absorbStartedAt < 3f) return;
        absorbFallbackDone = true;
        foreach (var ev in pending) ev.OnEventTriggered();
        foreach (var it in OrbInteractibles()) it.DisableInteractible();
        Plugin.Logger.LogWarning("[Cinématiques] absorption : interaction sans effet, évènement déclenché et orbe désactivée (repli)");
    }

    static void StartAbsorption()
    {
        if (!MainCharacterSingleton.TryGetEntity(out MotherBase.EntityFramework.Entity player) || player == null) return;
        var actor = player.GetComponentInChildren<MotherBase.ActionFramework.Actor>(true);
        if (actor == null) { Plugin.Logger.LogWarning("[Cinématiques] absorption : personnage sans Actor"); return; }

        // Actions candidates : celle que l'orbe fait jouer au personnage, et celles des touches du joueur.
        // La bonne est celle qui valide une interaction longue.
        var candidates = new List<MotherBase.ActionFramework.ActionTrigger>();
        foreach (var it in OrbInteractibles())
        {
            Plugin.Logger.LogInfo($"[Cinématiques] absorption : orbe '{it.gameObject.name}', personnage → '{it.interactorInteractionAction?.name}', orbe → '{it.selfInteractionAction?.name}', actif={it.enabled}");
            if (it.interactorInteractionAction != null) candidates.Add(it.interactorInteractionAction);
        }
        var input = player.GetComponentInChildren<MotherBase.ActionFramework.PlayerInput>(true);
        var binds = input?.data?.inputBindings;
        if (binds != null) foreach (var b in binds) if (b?.action != null) candidates.Add(b.action);

        MotherBase.ActionFramework.ActionTrigger trigger = null;
        MotherBase.ActionFramework.ActionData data = null;
        foreach (var t in candidates)
        {
            MotherBase.ActionFramework.ActionData d = null;
            try { d = t.GetActionData(player); } catch { }
            if (d != null && Feature<LongInteractValidationFeature>(d) != null) { trigger = t; data = d; break; }
        }
        Plugin.Logger.LogInfo($"[Cinématiques] absorption : actions en cours [{ActiveActionNames(actor)}], interaction longue '{data?.name}'");
        if (data == null) return;

        // Condition « touche tenue » suspendue le temps de l'absorption (rétablie à la sortie de l'état d'absorption)
        var features = data.actionFeatures;
        for (int i = 0; features != null && i < features.Count; i++)
        {
            var sc = features[i]?.TryCast<AStartConditionFeature>();
            if (sc == null || sc.TryCast<InputConditionFeature>() == null) continue;
            relaxed.Add((sc, sc.stopActionIfConditionNoLongerValid, sc.ignoreConditionForCanStart));
            sc.stopActionIfConditionNoLongerValid = false;
            sc.ignoreConditionForCanStart = true;
        }

        bool ok = actor.TryStartAction(player, trigger);
        if (!ok)
        {
            // Action d'attente en cours qui refuse d'être enchaînée : on l'interrompt avec l'interaction
            var active = actor.activeActions;
            for (int i = 0; !ok && active != null && i < active.Count; i++)
            {
                var a = active[i];
                if ((a?.data?.name ?? "").IndexOf("WaitForInput", StringComparison.OrdinalIgnoreCase) >= 0)
                    ok = actor.TryInterruptActionWith(player, a, trigger);
            }
        }
        Plugin.Logger.LogInfo($"[Cinématiques] absorption : interaction longue {(ok ? "lancée" : "refusée")}, actions [{ActiveActionNames(actor)}]");
    }

    static void EndAbsorb()
    {
        foreach (var (f, stop, ignore) in relaxed)
        {
            try { if (f != null) { f.stopActionIfConditionNoLongerValid = stop; f.ignoreConditionForCanStart = ignore; } }
            catch { }
        }
        relaxed.Clear();
        absorbStartedAt = -1f;
        absorbFallbackDone = false;
    }

    static T Feature<T>(MotherBase.ActionFramework.ActionData d) where T : MotherBase.ActionFramework.ActionFeature
    {
        var features = d.actionFeatures;
        for (int i = 0; features != null && i < features.Count; i++)
        {
            var f = features[i]?.TryCast<T>();
            if (f != null) return f;
        }
        return null;
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
        Each<MainObjectiveDeckAcquisitionManager>(c => c.CloseDeckAcquisition());
        return any;
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
        EndAbsorb();
    }
}
