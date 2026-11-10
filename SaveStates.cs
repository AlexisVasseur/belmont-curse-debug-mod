using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using BepInEx;
using Il2CppInterop.Runtime;
using MotherBase.ApplicationFlowManager;
using MotherBase.EntityFramework;
using RoomManager;
using UnityEngine;

namespace BelmontPractice;

/// <summary>Une savestate : la sauvegarde du jeu (GlobalSave) générée à la position courante.</summary>
public class SaveState
{
    public string Name { get; set; } = "";
    public string Level { get; set; } = "";
    public string Room { get; set; } = "";
    public System.Collections.Generic.List<MoverState> Movers { get; set; } = new();
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public bool HasRotation { get; set; }
    public float RX { get; set; }
    public float RY { get; set; }
    public float RZ { get; set; }
    public float RW { get; set; }
    public float Health { get; set; }
    // Valeurs lues directement sur le joueur (la sauvegarde du jeu ne garde pas les PV courants).
    public bool HasLiveStats { get; set; }
    public int LiveHealth { get; set; }
    public float LiveMana { get; set; }
    public float LivePotions { get; set; }
    public bool HasOverdrive { get; set; }
    public float LiveOverdrivePotions { get; set; }
    public float Mana { get; set; }
    public string Date { get; set; } = "";
    public string GlobalSaveJson { get; set; } = "";
    public string RuntimeWorldSaveJson { get; set; }

    // Calculée : ne pas sérialiser (les propriétés de Vector3 comme normalized bouclent à l'infini en JSON).
    [System.Text.Json.Serialization.JsonIgnore]
    public Vector3 Position => new(X, Y, Z);

    /// <summary>Téléportation : poser le joueur sur le sol s'il est juste en dessous (jamais pour une vraie savestate).</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public bool SnapToGround { get; set; }

    /// <summary>Rechargement complet du niveau même dans le niveau courant (combat de boss remis à zéro).</summary>
    public bool ForceFullReload { get; set; }

    /// <summary>Copie indépendante (le quickslot et un emplacement ne partagent jamais le même objet).</summary>
    public SaveState Clone() => JsonSerializer.Deserialize<SaveState>(JsonSerializer.Serialize(this));

    /// <summary>Nom par défaut, à la DebugMod : lieu + heure.</summary>
    public static string DefaultName(string level, string room)
    {
        string lvl = level.StartsWith("LevelID_") ? level.Substring("LevelID_".Length) : level;
        // "World_Biome_03_Catacombs_Catacombs_MVP_Room21_uid130" → "MVP_Room21"
        string r = System.Text.RegularExpressions.Regex.Replace(room ?? "", "_uid\\d+$", "");
        var parts = r.Split('_');
        string shortRoom = parts.Length >= 2 ? parts[^2] + "_" + parts[^1] : r;
        return $"{lvl} {shortRoom} {DateTime.Now:H:mm}".Trim();
    }
}

/// <summary>Capture et restauration via le système de sauvegarde du jeu (SaveLogic).</summary>
public static class GameBridge
{
    public static bool InGame(out string reason)
    {
        if (SaveLogic.Get() == null) { reason = "Game not ready"; return false; }
        if (!MainCharacterSingleton.TryGetEntity(out Entity e) || e == null) { reason = "No player: load a game first"; return false; }
        reason = null;
        return true;
    }

    /// <summary>
    /// Savestate de l'état actuel dont le point de départ est un autre endroit, éventuellement dans un autre niveau
    /// (téléportation depuis la carte). Dans un autre niveau, le chargement passe par le rechargement complet.
    /// </summary>
    public static SaveState CaptureForTeleport(LevelID level, Vector3 pos, bool fullReload = false)
    {
        var logic = SaveLogic.Get() ?? throw new Exception("SaveLogic introuvable");
        if (!MainCharacterSingleton.TryGetEntity(out Entity entity) || entity == null)
            throw new Exception("Joueur introuvable (es-tu en jeu ?)");
        LevelID current = logic._currentLevelIdVariable?.CurrentValue;
        if (level == null) level = current;
        bool otherLevel = level != null && current != null && level.name != current.name;

        logic.GeneratePlayerSaveWithPosition(entity, level, pos, SaveOrigin.DebugSpawn);
        DedupeObjectives(logic._currentSave);
        logic.SaveRoom(false);
        var save = logic._currentSave ?? throw new Exception("_currentSave est null");
        var rot = entity.transform.rotation;
        var state = new SaveState
        {
            Name = "Teleport",
            SnapToGround = true,
            ForceFullReload = fullReload,
            Level = level?.name ?? "",
            Room = "(teleport)", // autre salle : le jeu charge celle qui contient la destination
            X = pos.x, Y = pos.y, Z = pos.z,
            HasRotation = true, RX = rot.x, RY = rot.y, RZ = rot.z, RW = rot.w,
            Health = save.playerSave?.currentHealth ?? 0,
            Mana = save.playerSave?.currentMana ?? 0,
            Date = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            GlobalSaveJson = JsonUtility.ToJson(save),
            // L'état d'exécution du niveau actuel n'a pas de sens dans un autre niveau
            RuntimeWorldSaveJson = !otherLevel && logic._runtimeWorldSave != null ? JsonUtility.ToJson(logic._runtimeWorldSave) : null,
        };
        CaptureLiveStats(entity, state);
        Plugin.Logger.LogInfo($"[Téléportation] destination {pos} niveau '{state.Level}' ({(otherLevel ? "autre niveau : rechargement complet" : "même niveau")})");
        return state;
    }

    public static SaveState Capture(string name)
    {
        var logic = SaveLogic.Get() ?? throw new Exception("SaveLogic introuvable");
        if (!MainCharacterSingleton.TryGetEntity(out Entity entity) || entity == null)
            throw new Exception("Joueur introuvable (es-tu en jeu ?)");

        Vector3 pos = entity.transform.position;
        Quaternion rot = entity.transform.rotation;
        LevelID level = logic._currentLevelIdVariable?.CurrentValue;
        Plugin.Logger.LogInfo($"[Capture] niveau={level?.name ?? "null"} pos={pos}");

        // Met à jour la sauvegarde en mémoire avec l'état courant (sans l'écrire sur disque).
        logic.GeneratePlayerSaveWithPosition(entity, level, pos, SaveOrigin.DebugSpawn);
        DedupeObjectives(logic._currentSave);
        // false : ne pas sauvegarder les entités créées à l'exécution (elles se dupliquaient à chaque cycle).
        logic.SaveRoom(false);

        GlobalSave save = logic._currentSave ?? throw new Exception("_currentSave est null");
        string room = CurrentRoomName();
        var state = new SaveState
        {
            Name = string.IsNullOrWhiteSpace(name) ? SaveState.DefaultName(level?.name ?? "", room) : name,
            Level = level?.name ?? "",
            Room = room,
            X = pos.x, Y = pos.y, Z = pos.z,
            HasRotation = true, RX = rot.x, RY = rot.y, RZ = rot.z, RW = rot.w,
            Health = save.playerSave?.currentHealth ?? 0,
            Mana = save.playerSave?.currentMana ?? 0,
            Date = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            GlobalSaveJson = JsonUtility.ToJson(save),
            RuntimeWorldSaveJson = logic._runtimeWorldSave != null ? JsonUtility.ToJson(logic._runtimeWorldSave) : null,
        };
        CaptureLiveStats(entity, state);
        try { state.Movers = RoomObjects.Capture(RoomObjects.RoomRoots(CurrentRoomHandle())); }
        catch (Exception ex) { Plugin.Logger.LogWarning("[Capture] objets mobiles : " + ex.Message); }
        Plugin.Logger.LogInfo($"[Capture] salle '{state.Room}', {state.Movers.Count} objets mobiles, runtime={state.RuntimeWorldSaveJson?.Length ?? 0} caractères");
        Plugin.Logger.LogInfo($"[Capture] OK : {state.GlobalSaveJson.Length} caractères, save PV={state.Health} mana={state.Mana} | réel PV={state.LiveHealth} mana={state.LiveMana} potions={state.LivePotions}");
        return state;
    }

    static void CaptureLiveStats(Entity entity, SaveState state)
    {
        try
        {
            var dmg = entity.GetComponent<Damageable>();
            var mana = entity.GetComponent<ManaCaster>();
            var stats = entity.GetComponent<StatsStateComponent>();
            if (dmg == null) { Plugin.Logger.LogWarning("[Capture] Damageable introuvable sur le joueur"); return; }
            state.LiveHealth = dmg.currentHealth;
            if (mana != null) state.LiveMana = mana.GetCurrentMana(entity, mana.spellManaType);
            if (stats != null && stats.HasStat(Il2CppType.Of<HealthPotionCurrent>()))
                state.LivePotions = stats.GetStatObject(Il2CppType.Of<HealthPotionCurrent>()).GetFloat(entity);
            if (stats != null && stats.HasStat(Il2CppType.Of<HealthPotionOverdriveCurrent>()))
            {
                state.LiveOverdrivePotions = stats.GetStatObject(Il2CppType.Of<HealthPotionOverdriveCurrent>()).GetFloat(entity);
                state.HasOverdrive = true;
            }
            state.HasLiveStats = true;
        }
        catch (Exception ex) { Plugin.Logger.LogWarning("[Capture] stats réelles : " + ex.Message); }
    }

    /// <summary>Réapplique PV / mana / potions capturés sur le joueur.</summary>
    public static void ApplyLiveStats(SaveState state)
    {
        if (!state.HasLiveStats) return;
        if (!MainCharacterSingleton.TryGetEntity(out Entity entity) || entity == null) return;
        var dmg = entity.GetComponent<Damageable>();
        var mana = entity.GetComponent<ManaCaster>();
        var stats = entity.GetComponent<StatsStateComponent>();
        if (stats != null && stats.HasStat(Il2CppType.Of<HealthPotionCurrent>()))
            stats.SetFloat(Il2CppType.Of<HealthPotionCurrent>(), state.LivePotions);
        if (state.HasOverdrive && stats != null && stats.HasStat(Il2CppType.Of<HealthPotionOverdriveCurrent>()))
            stats.SetFloat(Il2CppType.Of<HealthPotionOverdriveCurrent>(), state.LiveOverdrivePotions);
        if (mana != null) mana.SetCurrentMana(entity, mana.spellManaType, state.LiveMana, true);
        if (dmg != null) dmg.SetCurrentHealth(entity, state.LiveHealth, true);
        RefreshPotionUI(entity);
        Cheats.RefreshBars(entity);
    }

    public static void RefreshPotionUIPublic(Entity e) => RefreshPotionUI(e);
    public static string CurrentRoomId() => CurrentRoomName();

    /// <summary>True pendant le fondu de transition de salle du jeu (écran de transition visible).</summary>
    public static bool RoomTransitionHudActive()
    {
        try
        {
            var hud = GetWorld()?.roomManager?._roomTransitionHUD;
            if (hud == null) return false;
            return hud.IsFadingIn || hud.IsFadingOut;
        }
        catch { return false; }
    }

    /// <summary>L'UI des potions n'écoute que des événements : on lui renvoie un événement d'initialisation.</summary>
    static void RefreshPotionUI(Entity entity)
    {
        try
        {
            var potions = entity.GetComponent<HealthPotionsComponent>();
            if (potions == null)
            {
                var found = UnityEngine.Object.FindObjectOfType(Il2CppType.Of<HealthPotionsComponent>());
                potions = found?.Cast<HealthPotionsComponent>();
            }
            if (potions == null) { Plugin.Logger.LogWarning("[UI] HealthPotionsComponent introuvable"); return; }

            var evt = new EntityInitHealthPotionsEvent
            {
                isPlayer = true,
                e = entity,
                maxPotionsCount = potions.maxHealthPotion,
                currentPotionsCount = potions.currentHealthPotion,
                currentOverdrivePotionsCount = potions.currentHealthPotionOverdrive,
            };
            var uis = UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<HealthPotionUIController>());
            foreach (var o in uis)
                o.Cast<HealthPotionUIController>().ProcessInitHealthPotionsEvent(evt);
            Plugin.Logger.LogInfo($"[UI] potions rafraîchies ({evt.currentPotionsCount}/{evt.maxPotionsCount}, overdrive {evt.currentOverdrivePotionsCount}) sur {uis.Length} UI");
        }
        catch (Exception ex) { Plugin.Logger.LogWarning("[UI] potions : " + ex.Message); }
    }

    // ---------- Sondes « le jeu est-il encore occupé ? » ----------

    /// <summary>
    /// Liste des raisons pour lesquelles l'image ne doit pas encore revenir (vide = tout est terminé).
    /// Chaque raison a un code court pour pouvoir ignorer celles déjà présentes avant le chargement.
    /// </summary>
    public static List<string> BusyReasons()
    {
        var r = new List<string>();
        if (!InGame(out _)) r.Add("pas_en_jeu");
        if (ApplicationFlowManager.IsLoading) r.Add("afm_chargement");
        if (RoomManagerSystem.IsLoadingNextRoom) r.Add("salle_suivante");
        if (RoomReloadInProgress) r.Add("rechargement_salle");
        try
        {
            var world = GetWorld();
            if (world != null)
            {
                if (world.IsLoading()) r.Add("monde_chargement");
                if (world._taskStarted) r.Add("monde_tache");
                var q = world._taskQueue;
                if (q != null && q.Count > 0) r.Add("monde_file");
                var hud = world.roomManager?._roomTransitionHUD;
                if (hud != null)
                {
                    if (hud.IsFadingIn || hud.IsFadingOut) r.Add("hud_fondu");
                    var img = hud.transitionFadeBlackScreen;
                    if (img != null && img.isActiveAndEnabled && img.color.a > 0.01f) r.Add("hud_visible");
                }
            }
        }
        catch (Exception ex) { r.Add("erreur_sonde:" + ex.Message); }
        if (CameraBlending()) r.Add("camera_transition");
        return r;
    }

    static Cinemachine.CinemachineBrain brain;

    public static bool CameraBlending()
    {
        try
        {
            if (brain == null)
            {
                var o = UnityEngine.Object.FindObjectOfType(Il2CppType.Of<Cinemachine.CinemachineBrain>());
                brain = o?.Cast<Cinemachine.CinemachineBrain>();
            }
            return brain != null && brain.IsBlending;
        }
        catch { brain = null; return false; }
    }

    /// <summary>
    /// Termine net la transition de caméra en cours (derrière le noir, elle ne sert à rien) :
    /// sans ça, chaque rechargement repart d'une transition inachevée et l'attente s'allonge.
    /// </summary>
    public static void CutCameraBlend()
    {
        try
        {
            if (!CameraBlending()) return;
            var frames = brain.mFrameStack;
            var active = brain.ActiveBlend;
            LoadSequence.Trace($"coupe caméra : {active?.Description?.Replace('\n', ' ')} durée={active?.Duration:0.00} écoulé={active?.TimeInBlend:0.00} couches={frames?.Count}");
            if (frames != null)
                for (int i = 0; i < frames.Count; i++)
                {
                    var b = frames[i]?.blend;
                    if (b != null && b.CamA != null) b.Duration = 0f;
                }
            brain.ActiveBlend = null;
        }
        catch (Exception ex) { Plugin.Logger.LogWarning("[Caméra] coupe : " + ex.Message); }
    }

    /// <summary>Force toutes les caméras virtuelles à se recaler sans lissage à la prochaine mise à jour.</summary>
    public static void SnapCameras()
    {
        try
        {
            var core = Cinemachine.CinemachineCore.Instance;
            int n = core.VirtualCameraCount;
            for (int i = 0; i < n; i++)
            {
                var vcam = core.GetVirtualCamera(i);
                if (vcam != null) vcam.PreviousStateIsValid = false;
            }
        }
        catch (Exception ex) { Plugin.Logger.LogWarning("[Caméra] " + ex.Message); }
    }

    // Les transitions de salle du jeu sont raccourcies pendant un chargement (elles se font derrière le noir).
    static float[] savedTransition;
    static bool savedResetBlend;

    public static void SpeedUpRoomTransitions()
    {
        try
        {
            savedResetBlend = RoomManagerSystem.ResetBlendOnRoomChange;
            RoomManagerSystem.ResetBlendOnRoomChange = true;
            var s = GetWorld()?.roomManager?.roomTransitionSettings;
            if (s == null || savedTransition != null) return;
            savedTransition = new[] { s.transitionInDuration, s.stayLockedDuration, s.stayUnlockedDuration, s.transitionOutDuration };
            s.transitionInDuration = s.stayLockedDuration = s.stayUnlockedDuration = s.transitionOutDuration = 0.01f;
        }
        catch (Exception ex) { Plugin.Logger.LogWarning("[Transition] " + ex.Message); }
    }

    public static void RestoreRoomTransitions()
    {
        try
        {
            RoomManagerSystem.ResetBlendOnRoomChange = savedResetBlend;
            var s = GetWorld()?.roomManager?.roomTransitionSettings;
            if (s == null || savedTransition == null) return;
            s.transitionInDuration = savedTransition[0];
            s.stayLockedDuration = savedTransition[1];
            s.stayUnlockedDuration = savedTransition[2];
            s.transitionOutDuration = savedTransition[3];
        }
        catch (Exception ex) { Plugin.Logger.LogWarning("[Transition] " + ex.Message); }
        finally { savedTransition = null; }
    }

    /// <summary>Menu plein écran du jeu ouvert (pause, carte, inventaire…) ?</summary>
    public static bool GameMenuOpen()
    {
        try
        {
            var states = GameStateManager.GetInstance()?._states;
            if (states == null) return false;
            foreach (var t in states.ToArray())
                if (t?.Item2 != null && t.Item2.name == "FullscreenMenuState") return true;
        }
        catch { }
        return false;
    }

    /// <summary>
    /// Ferme le menu plein écran du jeu s'il est ouvert. Charger une savestate menu ouvert laissait son état actif
    /// après le chargement (joueur figé en T-pose, « No current menu stack layer found »).
    /// Retourne true si une fermeture a été demandée (le chargement doit attendre qu'elle soit faite).
    /// </summary>
    public static bool CloseGameMenu()
    {
        if (!GameMenuOpen()) return false;
        try
        {
            var menu = UnityEngine.Object.FindObjectOfType(Il2CppType.Of<GameMenuManager>())?.TryCast<GameMenuManager>();
            if (menu == null) return false;
            menu.Close();
            Plugin.Logger.LogInfo("[Chargement] menu du jeu ouvert : fermé avant le chargement");
            return true;
        }
        catch (Exception ex) { Plugin.Logger.LogWarning("[Chargement] fermeture du menu : " + ex.Message); return false; }
    }

    public static bool PlayerHealthZero()
    {
        try
        {
            if (!MainCharacterSingleton.TryGetEntity(out Entity e) || e == null) return false;
            var dmg = e.GetComponent<Damageable>();
            return dmg != null && dmg.IsHealthZero();
        }
        catch { return false; }
    }

    public static Vector3 PlayerPosition() =>
        MainCharacterSingleton.TryGetEntity(out Entity e) && e != null ? e.transform.position : Vector3.zero;

    /// <summary>Remet le joueur exactement à la position de la savestate.</summary>
    public static void PlacePlayer(SaveState state)
    {
        if (MainCharacterSingleton.TryGetEntity(out Entity e) && e != null) PlacePlayer(e, state);
    }

    /// <summary>True si Restore utilisera le chargement rapide (même niveau).</summary>
    public static bool WillFastLoad(SaveState state) =>
        Plugin.FastLoad.Value && (SaveLogic.Get()?._currentLevelIdVariable?.CurrentValue?.name ?? "") == state.Level;

    /// <summary>Restaure une savestate. Retourne true si le chargement rapide (sans écran de chargement) a été utilisé.</summary>
    public static bool Restore(SaveState state)
    {
        var logic = SaveLogic.Get() ?? throw new Exception("SaveLogic introuvable");

        InjectSave(logic, state);
        if (!string.IsNullOrEmpty(state.RuntimeWorldSaveJson))
            logic._runtimeWorldSave = JsonUtility.FromJson(state.RuntimeWorldSaveJson, Il2CppType.Of<WorldSave>()).Cast<WorldSave>();
        Plugin.Logger.LogInfo("[Restore] sauvegarde injectée en mémoire");

        string currentLevel = logic._currentLevelIdVariable?.CurrentValue?.name ?? "";
        // Combat de boss / cinématique en cours : leurs états globaux ne sont remis à zéro que par
        // un rechargement complet (comme le « Réessayer » du jeu après une mort contre un boss).
        string unsafeReason = state.ForceFullReload ? "boss reset (whole level reloaded)" : BossHelper.UnsafeForFastLoad();
        if (unsafeReason != null)
        {
            Plugin.Logger.LogInfo($"[Restore] chargement complet forcé : {unsafeReason}");
            LoadSequence.Trace($"chargement complet forcé : {unsafeReason}");
        }
        if (Plugin.FastLoad.Value && unsafeReason == null && currentLevel == state.Level
            && MainCharacterSingleton.TryGetEntity(out Entity entity) && entity != null
            && RestoreInPlace(logic, entity, state))
            return true;
        Plugin.Logger.LogInfo($"[Restore] chargement complet (niveau actuel '{currentLevel}', savestate '{state.Level}')");
        SaveGuard.Arm(); // le rechargement complet peut faire écrire la sauvegarde du jeu sur le disque
        FullReload(state);
        return false;
    }

    static void InjectSave(SaveLogic logic, SaveState state)
    {
        var save = JsonUtility.FromJson(state.GlobalSaveJson, Il2CppType.Of<GlobalSave>()).Cast<GlobalSave>();
        DedupeObjectives(save);
        logic._currentSave = save;
    }

    /// <summary>
    /// Même niveau : relit l'état du joueur, le téléporte et force le rechargement de la salle.
    /// Retourne false (rien d'autre n'est fait) si le personnage garde des éléments absents de la savestate.
    /// </summary>
    static bool RestoreInPlace(SaveLogic logic, Entity entity, SaveState state)
    {
        // Les composants du joueur (PV, mana, inventaire, stats…) relisent la sauvegarde injectée.
        logic.LoadState(entity);
        Plugin.Logger.LogInfo("[Rapide] état du joueur relu");
        // Clés de progression posées depuis la capture (levier frappé…) : retirées, la salle rechargée les relit
        try
        {
            var removed = PlayerSaveCheck.RemoveExtraProgressionKeys(entity, state.GlobalSaveJson);
            if (removed.Count > 0) Plugin.Logger.LogInfo($"[Rapide] clés de progression retirées : {string.Join(", ", removed)}");
        }
        catch (Exception ex) { Plugin.Logger.LogWarning("[Rapide] clés de progression : " + ex.Message); }
        List<string> leftovers = null;
        try { leftovers = PlayerSaveCheck.Leftovers(logic, entity, state.GlobalSaveJson); }
        catch (Exception ex) { Plugin.Logger.LogWarning("[Rapide] vérification de l'état du joueur : " + ex.Message); }
        InjectSave(logic, state); // la vérification a regénéré la sauvegarde en mémoire
        if (leftovers != null && leftovers.Count > 0)
        {
            Plugin.Logger.LogInfo($"[Rapide] le personnage garde des éléments absents de la savestate, rechargement complet : {string.Join(" | ", leftovers)}");
            LoadSequence.Trace("éléments en trop après relecture : rechargement complet");
            return false;
        }
        Plugin.Logger.LogInfo("[Rapide] état du joueur conforme à la savestate");
        ApplyLiveStats(state);

        PlacePlayer(entity, state);
        pendingPlacement = state;
        Plugin.Logger.LogInfo($"[Rapide] joueur placé en {state.Position}");

        if (!Plugin.ReloadRoom.Value) return true;

        string currentRoom = CurrentRoomName();
        if (!string.IsNullOrEmpty(state.Room) && state.Room == currentRoom)
        {
            // Même salle : le jeu ne la recharge pas d'elle-même, on la décharge / recharge explicitement.
            StartSameRoomReload();
        }
        else
        {
            // Autre salle : le changement de salle la charge depuis zéro, comme un passage de porte.
            RoomManagerSystem.ForceTriggerRoomChange = true;
            Plugin.Logger.LogInfo($"[Rapide] changement de salle demandé ('{currentRoom}' → '{state.Room}')");
        }
        return true;
    }

    // ---------- Rechargement de la salle courante ----------

    static RoomStreamingHandle reloadHandle;
    static RedWineWorldInstance reloadWorld;
    static bool reloadSawUnloaded;
    static float reloadDeadline;
    static SaveState reloadState;

    /// <summary>True tant qu'un rechargement de salle lancé par le mod est en cours.</summary>
    public static bool RoomReloadInProgress => reloadHandle != null;

    static RedWineWorldInstance GetWorld()
    {
        var wi = SaveLogic.Get()?._worldInstanceVariable?.CurrentValue;
        return wi?.TryCast<RedWineWorldInstance>();
    }

    static RoomStreamingHandle CurrentRoomHandle() => GetWorld()?.roomManager?._currentRoomHandle;

    public static string CurrentLevelName() => SaveLogic.Get()?._currentLevelIdVariable?.CurrentValue?.name;

    /// <summary>Boss de la salle courante si c'est une arène (sinon null) ; arcana : personnage boss contenu, en repli.</summary>
    public static BossData CurrentRoomBoss(out string arcana)
    {
        arcana = null;
        try
        {
            var cfg = CurrentRoomHandle()?.worldRoomData?.roomData?.configuration?.TryCast<RedWineRoomConfiguration>();
            if (cfg == null || !cfg.isBossRoom) return null;
            var data = cfg.bossData ?? cfg.bossDataRef?.Asset?.TryCast<BossData>();
            var list = cfg.containedBossArcana;
            if (list != null && list.Count > 0) arcana = list[0]?.name;
            // BossData pas chargé (Mammon) : reconnu par l'identifiant de sa référence, puis par les arcanes absorbables
            if (data == null && arcana == null)
            {
                string guid = null;
                try { guid = cfg.bossDataRef?.AssetGUID; } catch { }
                if (!string.IsNullOrEmpty(guid)) arcana = Bosses.All.Find(b => b.AssetGuid == guid)?.Id?.name;
            }
            if (data == null && arcana == null && cfg.containedAbsorbableArcana != null && cfg.containedAbsorbableArcana.Count > 0)
                arcana = cfg.containedAbsorbableArcana[0]?.name;
            if (data == null && arcana == null) arcana = "?" + CurrentRoomName(); // arène sans boss identifiable
            return data;
        }
        catch { return null; }
    }

    static string CurrentRoomName()
    {
        try
        {
            var data = CurrentRoomHandle()?.worldRoomData;
            if (data == null) return "";
            return !string.IsNullOrEmpty(data.id) ? data.id : data.instanceName ?? "";
        }
        catch (Exception ex) { Plugin.Logger.LogWarning("[Salle] nom introuvable : " + ex.Message); return ""; }
    }

    static void StartSameRoomReload()
    {
        var world = GetWorld();
        var handle = world?.roomManager?._currentRoomHandle;
        if (world == null || handle == null)
        {
            Plugin.Logger.LogWarning("[Salle] monde ou salle courante introuvable, repli sur ForceTriggerRoomChange");
            RoomManagerSystem.ForceTriggerRoomChange = true;
            return;
        }
        world.Queue(new UnloadRoomTask(handle).Cast<ITask>());
        world.Queue(new LoadRoomTask(handle).Cast<ITask>());
        reloadHandle = handle;
        reloadWorld = world;
        reloadSawUnloaded = false;
        reloadDeadline = Time.unscaledTime + 10f;
        reloadState = pendingPlacement;
        Plugin.Logger.LogInfo($"[Salle] rechargement de la salle courante '{CurrentRoomName()}' (statut {handle.status})");
    }

    static SaveState pendingPlacement;

    /// <summary>À appeler à chaque frame : termine le rechargement de la salle courante quand elle est prête.</summary>
    public static void UpdateRoomReload()
    {
        if (reloadHandle == null) return;
        var status = reloadHandle.status;
        if (status != LoadingStatus.Initialized) reloadSawUnloaded = true;

        bool done = reloadSawUnloaded && status == LoadingStatus.Initialized && !reloadWorld.IsLoading();
        bool timeout = Time.unscaledTime > reloadDeadline;
        if (!done && !timeout) return;

        if (timeout) Plugin.Logger.LogWarning($"[Salle] délai dépassé (statut {status}, vu déchargée : {reloadSawUnloaded})");
        else Plugin.Logger.LogInfo("[Salle] salle rechargée");

        // Relance l'entrée dans la salle (caméras, machines à états, réactions d'entrée).
        RoomManagerSystem.ForceTriggerRoomChange = true;
        if (reloadState != null && MainCharacterSingleton.TryGetEntity(out Entity e) && e != null)
            PlacePlayer(e, reloadState);

        reloadHandle = null;
        reloadWorld = null;
        reloadState = null;
    }

    static void PlacePlayer(Entity entity, SaveState state)
    {
        Quaternion rot = state.HasRotation ? new Quaternion(state.RX, state.RY, state.RZ, state.RW) : entity.transform.rotation;
        var spawn = MainCharacterSpawnLogic.Get();
        if (spawn != null) spawn.PlaceMainCharacter(state.Position, rot);
        else entity.transform.SetPositionAndRotation(state.Position, rot);
    }

    const float GroundSnapDistance = 12f; // même portée que l'accroche aux miroirs

    /// <summary>
    /// Téléportation, salle chargée : cherche un sol jusqu'à GroundSnapDistance sous la destination (en ignorant
    /// les colliders du joueur et les zones déclencheuses) et y abaisse la destination. Sinon, rien ne change.
    /// </summary>
    public static void SnapToGround(SaveState state)
    {
        if (!state.SnapToGround) return;
        try
        {
            MainCharacterSingleton.TryGetEntity(out Entity player);
            var origin = state.Position + Vector3.up * 0.5f;
            var hits = Physics.RaycastAll(origin, Vector3.down, GroundSnapDistance + 0.5f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            Vector3 point = default;
            foreach (var h in hits)
            {
                var col = h.collider;
                if (col == null) continue;
                if (player != null && col.transform.IsChildOf(player.transform)) continue;
                if (h.distance < best) { best = h.distance; point = h.point; }
            }
            if (best == float.MaxValue) { Plugin.Logger.LogInfo($"[Téléportation] pas de sol sous {state.Position} : position gardée"); return; }
            Plugin.Logger.LogInfo($"[Téléportation] sol trouvé à {point.y:0.00} (destination {state.Y:0.00}) : joueur posé au sol");
            state.Y = point.y;
        }
        catch (Exception ex) { Plugin.Logger.LogWarning("[Téléportation] sol : " + ex.Message); }
    }

    /// <summary>Une fois la salle chargée : replace le joueur si le changement de salle l'a déplacé.</summary>
    public static void FixPositionAfterRoomLoad(SaveState state)
    {
        if (!MainCharacterSingleton.TryGetEntity(out Entity entity) || entity == null) return;
        Vector3 before = entity.transform.position;
        float dist = Vector3.Distance(before, state.Position);
        if (dist > 0.05f)
        {
            PlacePlayer(entity, state);
            Plugin.Logger.LogInfo($"[Position] joueur en {before} après chargement de salle (écart {dist:0.00}) → replacé en {state.Position}");
        }
        else Plugin.Logger.LogInfo($"[Position] OK ({before})");
    }

    /// <summary>
    /// Le jeu ajoute ses objectifs principaux à la liste à chaque génération de la sauvegarde du joueur,
    /// sans la vider : chaque capture la faisait grossir (180 000 entrées pour 7 objectifs, sauvegarde à 6,7 Mo).
    /// On ne garde qu'une occurrence de chaque, dans l'ordre.
    /// </summary>
    public static void DedupeObjectives(GlobalSave save)
    {
        try
        {
            var ps = save?.playerSave;
            var list = ps?.receivedMainObjectives;
            if (list == null || list.Length < 2) return;
            var seen = new HashSet<string>();
            var unique = new List<string>();
            for (int i = 0; i < list.Length; i++) { var s = list[i]; if (s != null && seen.Add(s)) unique.Add(s); }
            if (unique.Count == list.Length) return;
            ps.receivedMainObjectives = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStringArray(unique.ToArray());
            Plugin.Logger.LogInfo($"[Sauvegarde] objectifs en double retirés : {list.Length} → {unique.Count}");
        }
        catch (Exception ex) { Plugin.Logger.LogWarning("[Sauvegarde] doublons : " + ex.Message); }
    }

    static void FullReload(SaveState state)
    {
        if (Plugin.ForceSpawnPosition.Value)
        {
            var spawn = MainCharacterSpawnLogic.Get();
            if (spawn != null)
            {
                spawn.SetForcePosition(state.Position, null);
                Plugin.Logger.LogInfo($"[Restore] position forcée : {state.Position}");
            }
        }

        var afmObj = UnityEngine.Object.FindObjectOfType(Il2CppType.Of<ApplicationFlowManager>());
        if (afmObj == null) throw new Exception("ApplicationFlowManager introuvable");
        var afm = afmObj.Cast<ApplicationFlowManager>();
        // Autre niveau : charger l'état de jeu de ce niveau (recharger l'état courant ramènerait au niveau actuel)
        string current = SaveLogic.Get()?._currentLevelIdVariable?.CurrentValue?.name ?? "";
        if (!string.IsNullOrEmpty(state.Level) && state.Level != current)
        {
            var levelState = FindLevelState(state.Level);
            if (levelState == null) throw new Exception($"game state of level '{state.Level}' not found");
            Plugin.Logger.LogInfo($"[Restore] chargement de l'état '{levelState.name}' (au lieu de '{afm.GetCurrentState()?.name}')");
            afm.LoadState(levelState);
            return;
        }
        Plugin.Logger.LogInfo($"[Restore] rechargement de l'état '{afm.GetCurrentState()?.name}'");
        afm.ReloadCurrentState();
    }

    /// <summary>
    /// État de jeu (ApplicationState) d'un autre niveau : il porte le nom du niveau sans « LevelID_ »
    /// (LevelID_03_Catacombs → 03_Catacombs). Null si c'est le niveau actuel ou s'il est introuvable.
    /// (La table niveau → état du jeu passe par un paramètre de sortie structure qui faisait planter le jeu.)
    /// </summary>
    static MotherBase.ApplicationFlowManager.ApplicationState FindLevelState(string levelName)
    {
        try
        {
            string current = SaveLogic.Get()?._currentLevelIdVariable?.CurrentValue?.name ?? "";
            if (string.IsNullOrEmpty(levelName) || levelName == current) return null;
            string wanted = levelName.StartsWith("LevelID_") ? levelName.Substring("LevelID_".Length) : levelName;
            foreach (var o in Resources.FindObjectsOfTypeAll(Il2CppType.Of<MotherBase.ApplicationFlowManager.ApplicationState>()))
            {
                var st = o.TryCast<MotherBase.ApplicationFlowManager.ApplicationState>();
                if (st != null && st.name == wanted) return st;
            }
            Plugin.Logger.LogWarning($"[Restore] état de jeu '{wanted}' introuvable pour le niveau '{levelName}'");
        }
        catch (Exception ex) { Plugin.Logger.LogWarning("[Restore] état du niveau : " + ex.Message); }
        return null;
    }
    /// <summary>Une fois la salle chargée : remet les objets mobiles (scies, plateformes) où ils étaient.</summary>
    public static void RestoreRoomObjects(SaveState state)
    {
        try { RoomObjects.Restore(RoomObjects.RoomRoots(CurrentRoomHandle()), state.Movers); }
        catch (Exception ex) { Plugin.Logger.LogWarning("[Objets] " + ex.Message); }
    }

    public static void RearmBossTriggers()
    {
        try { BossHelper.RearmTriggers(RoomObjects.RoomRoots(CurrentRoomHandle()), PlayerPosition()); }
        catch (Exception ex) { Plugin.Logger.LogWarning("[Boss] " + ex.Message); }
    }

    public static string DumpRoom() =>
        RoomObjects.Dump(RoomObjects.RoomRoots(CurrentRoomHandle()), SlotStore.Folder);

    /// <summary>Écrit la sauvegarde en mémoire actuelle dans un fichier, pour comparer avec celle du disque.</summary>
    public static string DumpCurrentSave()
    {
        var logic = SaveLogic.Get() ?? throw new Exception("SaveLogic introuvable");
        string path = Path.Combine(SlotStore.Folder, $"dump_{DateTime.Now:yyyyMMdd_HHmmss}.json");
        File.WriteAllText(path, JsonUtility.ToJson(logic._currentSave, true));
        return path;
    }
}

