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
    public static SaveState CaptureForTeleport(LevelID level, Vector3 pos)
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

        var save = JsonUtility.FromJson(state.GlobalSaveJson, Il2CppType.Of<GlobalSave>()).Cast<GlobalSave>();
        DedupeObjectives(save);
        logic._currentSave = save;
        if (!string.IsNullOrEmpty(state.RuntimeWorldSaveJson))
            logic._runtimeWorldSave = JsonUtility.FromJson(state.RuntimeWorldSaveJson, Il2CppType.Of<WorldSave>()).Cast<WorldSave>();
        Plugin.Logger.LogInfo("[Restore] sauvegarde injectée en mémoire");

        string currentLevel = logic._currentLevelIdVariable?.CurrentValue?.name ?? "";
        // Combat de boss / cinématique en cours : leurs états globaux ne sont remis à zéro que par
        // un rechargement complet (comme le « Réessayer » du jeu après une mort contre un boss).
        string unsafeReason = BossHelper.UnsafeForFastLoad();
        if (unsafeReason != null)
        {
            Plugin.Logger.LogInfo($"[Restore] chargement complet forcé : {unsafeReason}");
            LoadSequence.Trace($"chargement complet forcé : {unsafeReason}");
        }
        if (Plugin.FastLoad.Value && unsafeReason == null && currentLevel == state.Level
            && MainCharacterSingleton.TryGetEntity(out Entity entity) && entity != null)
        {
            RestoreInPlace(logic, entity, state);
            return true;
        }
        Plugin.Logger.LogInfo($"[Restore] chargement complet (niveau actuel '{currentLevel}', savestate '{state.Level}')");
        SaveGuard.Arm(); // le rechargement complet peut faire écrire la sauvegarde du jeu sur le disque
        FullReload(state);
        return false;
    }

    /// <summary>Même niveau : relit l'état du joueur, le téléporte et force le rechargement de la salle.</summary>
    static void RestoreInPlace(SaveLogic logic, Entity entity, SaveState state)
    {
        // Les composants du joueur (PV, mana, inventaire, stats…) relisent la sauvegarde injectée.
        logic.LoadState(entity);
        Plugin.Logger.LogInfo("[Rapide] état du joueur relu");
        ApplyLiveStats(state);

        PlacePlayer(entity, state);
        pendingPlacement = state;
        Plugin.Logger.LogInfo($"[Rapide] joueur placé en {state.Position}");

        if (!Plugin.ReloadRoom.Value) return;

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
        if (TryFindLevelState(state.Level, out var stateId) && afm.HasState(stateId))
        {
            Plugin.Logger.LogInfo($"[Restore] chargement de l'état du niveau '{state.Level}' (au lieu de '{afm.GetCurrentState()?.name}')");
            afm.LoadState(stateId);
            return;
        }
        Plugin.Logger.LogInfo($"[Restore] rechargement de l'état '{afm.GetCurrentState()?.name}'");
        afm.ReloadCurrentState();
    }

    /// <summary>État de jeu (ApplicationState) d'un niveau, via la table niveau → état du jeu. False si c'est le niveau actuel ou introuvable.</summary>
    static bool TryFindLevelState(string levelName, out MotherBase.ApplicationFlowManager.ApplicationStateID stateId)
    {
        stateId = default;
        try
        {
            string current = SaveLogic.Get()?._currentLevelIdVariable?.CurrentValue?.name ?? "";
            if (string.IsNullOrEmpty(levelName) || levelName == current) return false;
            LevelID level = null;
            foreach (var o in Resources.FindObjectsOfTypeAll(Il2CppType.Of<LevelID>()))
            {
                var l = o.TryCast<LevelID>();
                if (l != null && l.name == levelName) { level = l; break; }
            }
            if (level == null) { Plugin.Logger.LogWarning($"[Restore] niveau '{levelName}' introuvable"); return false; }
            foreach (var o in Resources.FindObjectsOfTypeAll(Il2CppType.Of<LevelApplicationStateDictionary>()))
            {
                var dict = o.TryCast<LevelApplicationStateDictionary>();
                if (dict != null && dict.TryFindStateFromLevelGuid(level.guid, out stateId)) return true;
            }
            Plugin.Logger.LogWarning($"[Restore] état de jeu du niveau '{levelName}' introuvable");
        }
        catch (Exception ex) { Plugin.Logger.LogWarning("[Restore] état du niveau : " + ex.Message); }
        return false;
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

