using System;
using Il2CppInterop.Runtime;
using MotherBase.ActionFramework;
using MotherBase.EntityFramework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BelmontPractice;

/// <summary>Utilitaires : PV / mana / potions / sauts infinis, noclip.</summary>
public static class Cheats
{
    public static bool InfiniteHealth, LockHealth, InfiniteMana, InfinitePotions, InfiniteJump, Noclip;
    static int? lockedHealth; // PV figés (LockHealth), relevés à l'activation
    // PV figés : un changement (soin, dégâts) s'applique, puis les PV reviennent à la valeur figée une fois stables
    const float LockRestoreDelay = 1f;
    static int lockSeenHealth = -1;
    static int lockSeenPotions = -1; // diagnostic : potion bue pendant le verrouillage ?
    static float lockChangedAt;

    static bool clampApplied;
    static float savedClampMin;
    static bool noclipActive;
    static string noclipRoom = "";

    /// <summary>Point de réapparition après un piège (scie…) = position actuelle. Sans sol en noclip, le jeu ne le met plus à jour.</summary>
    static void SetSafeRespawnHere(Entity e)
    {
        var lsp = e.GetComponent<LastStablePosition>();
        if (lsp == null) return;
        var pos = e.transform.position;
        lsp.lastSafeRespawnPosition = pos;
        lsp.lastGroundStablePosition = pos;
    }
    static Vector3 noclipPos;
    static float nextJumpLog;
    static float nextPotionUiRefresh;

    static bool Player(out Entity e) => MainCharacterSingleton.TryGetEntity(out e) && e != null;

    /// <summary>
    /// PV actuels : la stat HealthCurrent (celle du HUD, modifiée par les soins et les dégâts) ; à défaut, le champ du
    /// Damageable. dmg null : la stat seule (-1 si absente).
    /// </summary>
    static int Health(Entity e, StatsStateComponent stats, Damageable dmg)
    {
        var hc = Il2CppType.Of<HealthCurrent>();
        if (stats != null && stats.HasStat(hc)) return Mathf.RoundToInt(stats.GetStatObject(hc).GetFloat(e));
        return dmg != null ? dmg.currentHealth : -1;
    }

    /// <summary>Appelé à chaque frame (Update), en jeu.</summary>
    public static void Update()
    {
        if (!Player(out var e)) { noclipActive = false; clampApplied = false; return; }
        var stats = e.GetComponent<StatsStateComponent>();

        // --- PV infinis : plancher de PV à 1 (pas de mort) + remplissage ; PV figés : plancher + retour à la valeur relevée ---
        try
        {
            var clampType = Il2CppType.Of<HealthClampMin>();
            var dmg = e.GetComponent<Damageable>();
            if (!LockHealth) { lockedHealth = null; lockSeenHealth = -1; }
            else if (dmg != null && lockedHealth == null)
            {
                lockedHealth = dmg.currentHealth;
                Plugin.Logger.LogInfo($"[PV figés] activé : {lockedHealth} (stat {Health(e, stats, null)}, Damageable {dmg.currentHealth}, max {dmg.maxHealth})");
            }
            if ((InfiniteHealth || LockHealth) && stats != null && stats.HasStat(clampType))
            {
                if (!clampApplied)
                {
                    savedClampMin = stats.GetStatObject(clampType).GetFloat(e);
                    clampApplied = true;
                    Plugin.Logger.LogInfo($"[PV] plancher d'origine : {savedClampMin}");
                }
                // PV figés : plancher à 1 / max. Avec 1, les PV remontaient au maximum dès l'activation (le plancher semble
                // être une part du maximum) ; PV infinis : 1 (remplis de toute façon)
                float floor = lockedHealth.HasValue && dmg != null && dmg.maxHealth > 0 ? 1f / dmg.maxHealth : 1f;
                // Écrit seulement s'il change : réécrire une stat à chaque frame la signale modifiée en permanence
                if (Mathf.Abs(stats.GetStatObject(clampType).GetFloat(e) - floor) > 1e-5f) stats.SetFloat(clampType, floor);
                if (dmg != null && lockedHealth.HasValue)
                {
                    // Le soin d'une potion (ou des dégâts) s'applique normalement ; une fois les PV stables depuis
                    // LockRestoreDelay, ils sont remis à la valeur figée. Figés au maximum : gardés à 1 de moins, le jeu
                    // refusant la potion à PV pleins
                    int hold = lockedHealth.Value >= dmg.maxHealth && dmg.maxHealth > 1 ? dmg.maxHealth - 1 : lockedHealth.Value;
                    int hp = dmg.currentHealth; // PV affichés (HUD, panneau)
                    int potions = Mathf.RoundToInt(stats.HasStat(Il2CppType.Of<HealthPotionCurrent>()) ? stats.GetStatObject(Il2CppType.Of<HealthPotionCurrent>()).GetFloat(e) : -1);
                    if (potions != lockSeenPotions)
                    {
                        if (lockSeenPotions >= 0) Plugin.Logger.LogInfo($"[PV figés] potions {lockSeenPotions} → {potions} (PV {hp})");
                        lockSeenPotions = potions;
                    }
                    if (hp != lockSeenHealth)
                    {
                        if (lockSeenHealth >= 0) Plugin.Logger.LogInfo($"[PV figés] {lockSeenHealth} → {hp} (stat {Health(e, stats, null)}, max {dmg.maxHealth}, figés à {hold})");
                        lockSeenHealth = hp;
                        lockChangedAt = Time.unscaledTime;
                    }
                    if (hp != hold && Time.unscaledTime - lockChangedAt >= LockRestoreDelay)
                    {
                        dmg.SetCurrentHealth(e, hold, true);
                        // Comme les boutons du panneau : stat lue par le HUD et barres mises à jour aussi
                        var hc = Il2CppType.Of<HealthCurrent>();
                        if (stats.HasStat(hc)) stats.SetFloat(hc, hold);
                        RefreshBars(e);
                        lockSeenHealth = dmg.currentHealth;
                        Plugin.Logger.LogInfo($"[PV figés] remis à {hold} (lu ensuite : {lockSeenHealth}, stat {Health(e, stats, null)})");
                    }
                }
                else if (dmg != null && dmg.currentHealth < dmg.maxHealth) dmg.SetCurrentHealth(e, dmg.maxHealth, true);
            }
            else if (clampApplied)
            {
                if (stats != null) stats.SetFloat(clampType, savedClampMin);
                clampApplied = false;
            }
        }
        catch (Exception ex) { Warn("PV", ex); InfiniteHealth = LockHealth = false; }

        // --- Mana infinie ---
        try
        {
            if (InfiniteMana)
            {
                var mana = e.GetComponent<ManaCaster>();
                if (mana != null)
                {
                    int max = mana.GetMaxMana(e, mana.spellManaType);
                    if (mana.GetCurrentMana(e, mana.spellManaType) < max) mana.SetCurrentMana(e, mana.spellManaType, max, false);
                }
            }
        }
        catch (Exception ex) { Warn("mana", ex); InfiniteMana = false; }

        // --- Potions infinies ---
        try
        {
            if (InfinitePotions && stats != null)
            {
                var cur = Il2CppType.Of<HealthPotionCurrent>();
                var max = Il2CppType.Of<HealthPotionMax>();
                if (stats.HasStat(cur) && stats.HasStat(max))
                {
                    float m = stats.GetStatObject(max).GetFloat(e);
                    float c = stats.GetStatObject(cur).GetFloat(e);
                    if (c < m) stats.SetFloat(cur, m);
                    // L'interface affiche la potion bue après coup : on la resynchronise régulièrement
                    if (c < m || Time.unscaledTime >= nextPotionUiRefresh)
                    {
                        GameBridge.RefreshPotionUIPublic(e);
                        nextPotionUiRefresh = Time.unscaledTime + 0.3f;
                    }
                }
            }
        }
        catch (Exception ex) { Warn("potions", ex); InfinitePotions = false; }

        // --- Sauts infinis : le compteur de sauts en l'air est rechargé en permanence ---
        try
        {
            if (InfiniteJump)
            {
                var move = e.GetComponent<CharacterMovementState>();
                if (move != null)
                {
                    // Saut en l'air disponible (double saut)…
                    if (move.availableAirJumps < 1) move.availableAirJumps = Math.Max(1, move.airJumps);
                    // …et « vient de quitter le sol » : le saut normal reste permis (tolérance de bord / coyote time)
                    if (move.stableFrames == 0) move.unstableFrames = 0;
                    if (Time.unscaledTime >= nextJumpLog)
                    {
                        nextJumpLog = Time.unscaledTime + 1f;
                        Plugin.Logger.LogInfo($"[Sauts] airJumps={move.airJumps} dispo={move.availableAirJumps} stable={move.stableFrames} instable={move.unstableFrames} chute={move.fallingFrames} état={move.locomotionState}");
                    }
                }
                else if (Time.unscaledTime >= nextJumpLog) { nextJumpLog = Time.unscaledTime + 5f; Plugin.Logger.LogWarning("[Sauts] CharacterMovementState introuvable"); }
            }
        }
        catch (Exception ex) { Warn("sauts", ex); InfiniteJump = false; }

        // --- Noclip : bascule des collisions ---
        try
        {
            if (Noclip != noclipActive)
            {
                var motor = e.GetComponent<CharacterMotor>();
                SetPhysics(motor, !Noclip);
                noclipSuspended = false;
                SetEdgeGrabBlock(e, Noclip);
                // Le moteur réimpose sa position : on pilote plutôt la vitesse via le composant VelocityOverride du jeu
                if (Noclip) e.AddComponentDataIfNone(Il2CppType.Of<VelocityOverride>());
                else e.RemoveComponentDataIfAny(Il2CppType.Of<VelocityOverride>());
                noclipPos = e.transform.position;
                noclipActive = Noclip;
                noclipRoom = GameBridge.CurrentRoomId();
                if (!Noclip) SetSafeRespawnHere(e); // en sortant du noclip, on réapparaît ici
                Plugin.Logger.LogInfo($"[Utilitaires] noclip {(Noclip ? "activé" : "désactivé")} (moteur {(motor != null ? "trouvé" : "introuvable")})");
            }
        }
        catch (Exception ex) { Warn("noclip", ex); Noclip = false; }
    }

    static bool noclipSuspended; // noclip mis en pause pendant une transition de salle
    static float noclipResumeAt; // fin de transition : le jeu finit de placer le perso avant la reprise
    const float TransitionGrace = 0.5f;

    // Part du noclip dans le drapeau « pas d'agrippe aux rebords » (compteur partagé avec les actions du jeu)
    static PreventEdgeGrabFlag edgeFlag;
    static bool edgeFlagCreated;

    /// <summary>
    /// Noclip : le personnage ne s'agrippe plus aux rebords (passages verticaux où Rose s'accroche à la paroi : l'action
    /// d'agrippe réactivait les collisions, que le noclip recoupait, et Rose restait suspendue).
    /// </summary>
    static void SetEdgeGrabBlock(Entity e, bool on)
    {
        try
        {
            if (on)
            {
                if (edgeFlag != null && edgeFlag.WasCollected) edgeFlag = null;
                if (edgeFlag != null) return;
                edgeFlagCreated = e.GetComponent<PreventEdgeGrabFlag>() == null;
                edgeFlag = e.AddComponentDataIfNone(Il2CppType.Of<PreventEdgeGrabFlag>())?.TryCast<PreventEdgeGrabFlag>();
                if (edgeFlag != null) edgeFlag.flagCounter++;
            }
            else if (edgeFlag != null)
            {
                if (!edgeFlag.WasCollected)
                {
                    edgeFlag.flagCounter = Math.Max(0, edgeFlag.flagCounter - 1);
                    if (edgeFlag.flagCounter == 0 && edgeFlagCreated) e.RemoveComponentDataIfAny(Il2CppType.Of<PreventEdgeGrabFlag>());
                }
                edgeFlag = null;
            }
        }
        catch (Exception ex) { Plugin.Logger.LogWarning("[Utilitaires] noclip, agrippe aux rebords : " + ex.Message); }
    }

    /// <summary>Collisions et détection du sol du personnage (false : noclip).</summary>
    static void SetPhysics(CharacterMotor motor, bool on)
    {
        if (motor == null) return;
        motor.SetCapsuleCollisionsActivation(on);
        motor.SetMovementCollisionsSolvingActivation(on);
        motor.SetGroundSolvingActivation(on);
    }

    /// <summary>Appelé en début d'image (Update), avant que le moteur du jeu ne déplace le personnage.</summary>
    public static void ApplyNoclip()
    {
        if (!noclipActive || !Player(out var e)) return;
        try
        {
            var motor = e.GetComponent<CharacterMotor>();
            // Transition de salle (ex. passage vertical où Rose s'agrippe) : le jeu déplace le perso lui-même, avec
            // collisions et sol (sans eux, la gravité revenue le faisait retomber à travers le sol dans la salle d'avant)
            bool transition = RoomManager.RoomManagerSystem.IsLoadingNextRoom || GameBridge.RoomTransitionHudActive();
            SetEdgeGrabBlock(e, true); // recréé si le jeu a retiré le composant
            if (transition) noclipResumeAt = Time.unscaledTime + TransitionGrace;
            if (transition || (noclipSuspended && Time.unscaledTime < noclipResumeAt))
            {
                if (!noclipSuspended)
                {
                    noclipSuspended = true;
                    e.RemoveComponentDataIfAny(Il2CppType.Of<VelocityOverride>());
                    SetPhysics(motor, true);
                    Plugin.Logger.LogInfo($"[Utilitaires] noclip suspendu pendant la transition de salle, depuis {e.transform.position} ({GameBridge.CurrentRoomId()})");
                }
                return;
            }
            if (noclipSuspended)
            {
                noclipSuspended = false;
                SetPhysics(motor, false);
                Plugin.Logger.LogInfo($"[Utilitaires] noclip repris après la transition de salle, en {e.transform.position} ({GameBridge.CurrentRoomId()})");
            }
            // Le jeu réactive collisions / sol de lui-même (réapparition après un piège, dégâts…) : on les recoupe
            else if (motor != null && (motor.solveMovementCollisions || motor.solveGrounding))
            {
                SetPhysics(motor, false);
                Plugin.Logger.LogInfo($"[Utilitaires] noclip : collisions réactivées par le jeu, recoupées (en {e.transform.position})");
            }
            // Jamais « au sol » : sinon le jeu garde l'état au sol (détection du sol coupée, il n'est plus remis à jour),
            // retire la vitesse verticale et il fallait sauter pour décoller
            if (motor != null)
            {
                motor.ForceUnground(0.1f);
                var g = motor.groundingStatus;
                if (g.isStableOnGround || g.foundAnyGround)
                {
                    g.isStableOnGround = false;
                    g.foundAnyGround = false;
                    motor.groundingStatus = g;
                }
            }
            // Nouvelle salle : elle devient le point de réapparition des pièges
            string room = GameBridge.CurrentRoomId();
            if (room != noclipRoom && room.Length > 0)
            {
                noclipRoom = room;
                SetSafeRespawnHere(e);
                Plugin.Logger.LogInfo($"[Utilitaires] noclip : réapparition fixée dans {room}");
            }

            var kb = Keyboard.current;
            Vector2 dir = new(0f, 0f);
            if (kb != null)
            {
                // Touches physiques W/A/S/D (= Z/Q/S/D en AZERTY) et flèches
                if (kb[Key.W].isPressed || kb[Key.UpArrow].isPressed) dir.y += 1f;
                if (kb[Key.S].isPressed || kb[Key.DownArrow].isPressed) dir.y -= 1f;
                if (kb[Key.A].isPressed || kb[Key.LeftArrow].isPressed) dir.x -= 1f;
                if (kb[Key.D].isPressed || kb[Key.RightArrow].isPressed) dir.x += 1f;
            }
            var pad = Gamepad.current;
            if (pad != null)
            {
                Vector2 stick = pad.leftStick.ReadValue();
                if (Mathf.Abs(stick.x) > 0.2f) dir.x += stick.x;
                if (Mathf.Abs(stick.y) > 0.2f) dir.y += stick.y;
                // Croix directionnelle
                if (pad.dpad.up.isPressed) dir.y += 1f;
                if (pad.dpad.down.isPressed) dir.y -= 1f;
                if (pad.dpad.left.isPressed) dir.x -= 1f;
                if (pad.dpad.right.isPressed) dir.x += 1f;
            }
            float speed = Plugin.NoclipSpeed.Value * (kb != null && kb[Key.LeftShift].isPressed ? 3f : 1f);
            if (dir.sqrMagnitude > 1f) dir = dir.normalized;
            var vo = e.AddComponentDataIfNone(Il2CppType.Of<VelocityOverride>())?.TryCast<VelocityOverride>();
            if (vo != null) vo.value = new Vector3(dir.x * speed, dir.y * speed, 0f); // vitesse nulle = le perso flotte
        }
        catch (Exception ex) { Warn("noclip", ex); Noclip = false; }
    }

    /// <summary>Après un chargement de savestate : le noclip repart de la nouvelle position.</summary>
    public static void ResyncNoclip()
    {
        if (noclipActive && Player(out var e)) noclipPos = e.transform.position;
    }

    // ---------- Vie ----------

    /// <summary>PV actuels et max ; false hors jeu.</summary>
    public static bool HealthValues(out float cur, out float max)
    {
        cur = max = 0f;
        try
        {
            if (!Player(out var e)) return false;
            var dmg = e.GetComponent<Damageable>();
            if (dmg == null) return false;
            cur = dmg.currentHealth;
            max = dmg.maxHealth;
            return true;
        }
        catch { return false; }
    }

    /// <summary>Ajoute delta aux PV (entre 1 et le max) ; null = soin complet.</summary>
    public static string ChangeHealth(int? delta)
    {
        try
        {
            if (!Player(out var e)) return "Unavailable (load a game first)";
            var dmg = e.GetComponent<Damageable>();
            if (dmg == null) return "Health not found";
            int target = delta == null ? dmg.maxHealth : Math.Clamp(dmg.currentHealth + delta.Value, 1, dmg.maxHealth);
            dmg.SetCurrentHealth(e, target, true);
            var stats = e.GetComponent<StatsStateComponent>();
            var hc = Il2CppType.Of<HealthCurrent>();
            if (stats != null && stats.HasStat(hc)) stats.SetFloat(hc, target);
            RefreshBars(e);
            string msg = delta == null ? $"Healed: {target} / {dmg.maxHealth}" : $"Health: {target} / {dmg.maxHealth}";
            if (LockHealth) { lockedHealth = target; msg += " (locked)"; } // les boutons fixent la valeur figée
            else if (InfiniteHealth && target < dmg.maxHealth) msg += " (Infinite HP will refill it)";
            return msg;
        }
        catch (Exception ex) { return "Health change failed: " + ex.Message; }
    }

    // ---------- Mana ----------

    /// <summary>Mana actuelle et max ; false hors jeu.</summary>
    public static bool ManaValues(out float cur, out float max)
    {
        cur = max = 0f;
        try
        {
            if (!Player(out var e)) return false;
            var mana = e.GetComponent<ManaCaster>();
            if (mana == null) return false;
            cur = Mathf.Round(mana.GetCurrentMana(e, mana.spellManaType));
            max = mana.GetMaxMana(e, mana.spellManaType);
            return true;
        }
        catch { return false; }
    }

    /// <summary>Ajoute delta à la mana (entre 0 et le max) ; null = remplissage complet.</summary>
    public static string ChangeMana(int? delta)
    {
        try
        {
            if (!Player(out var e)) return "Unavailable (load a game first)";
            var mana = e.GetComponent<ManaCaster>();
            if (mana == null) return "Mana not found";
            int max = mana.GetMaxMana(e, mana.spellManaType);
            float cur = mana.GetCurrentMana(e, mana.spellManaType);
            float target = delta == null ? max : Mathf.Clamp(Mathf.Round(cur) + delta.Value, 0f, max);
            mana.SetCurrentMana(e, mana.spellManaType, target, false);
            mana.TriggerManaChangeReaction(e, mana.spellManaType, cur, target);
            RefreshBars(e, cur);
            string msg = delta == null ? $"Mana refilled: {Mathf.RoundToInt(target)} / {max}" : $"Mana: {Mathf.RoundToInt(target)} / {max}";
            if (InfiniteMana && target < max) msg += " (Infinite mana will refill it)";
            return msg;
        }
        catch (Exception ex) { return "Mana change failed: " + ex.Message; }
    }

    // ---------- Potions ----------

    /// <summary>Composant des potions du joueur : ses références de stats sont celles que lit le HUD.</summary>
    static HealthPotionsComponent Potions(Entity e)
    {
        var p = e.GetComponent<HealthPotionsComponent>();
        if (p != null) return p;
        return UnityEngine.Object.FindObjectOfType(Il2CppType.Of<HealthPotionsComponent>())?.Cast<HealthPotionsComponent>();
    }

    /// <summary>Plafond des potions bonus (stat HealthPotionOverdriveMax, cherchée là où vivent les stats des potions).</summary>
    static int BonusMax(Entity e, HealthPotionsComponent p)
    {
        var t = Il2CppType.Of<HealthPotionOverdriveMax>();
        foreach (var s in new[] { p._currentHealthPotionOverdriveStat?._statsStateComponent, e.GetComponent<StatsStateComponent>() })
            if (s != null && s.HasStat(t)) return Mathf.RoundToInt(s.GetStatObject(t).GetFloat(p._currentHealthPotionOverdriveStat?._e ?? e));
        return 9; // limite inconnue
    }

    /// <summary>
    /// Potions normales (max 3 par défaut) et bonus (lâchées par les ennemis, au-delà du max).
    /// Dans le jeu, HealthPotionCurrent est le TOTAL (normales + bonus) et HealthPotionOverdriveCurrent la part bonus.
    /// </summary>
    public static bool PotionValues(out int cur, out int max, out int bonus, out int bonusMax)
    {
        cur = max = bonus = bonusMax = 0;
        try
        {
            if (!Player(out var e)) return false;
            var p = Potions(e);
            if (p == null) return false;
            int total = p.currentHealthPotion;
            max = p.maxHealthPotion;
            bonus = Math.Clamp(p.currentHealthPotionOverdrive, 0, total);
            cur = total - bonus;
            bonusMax = BonusMax(e, p);
            return true;
        }
        catch { return false; }
    }

    /// <summary>Fixe le total de potions : d'abord les normales jusqu'au max, le reste en bonus.</summary>
    public static string SetPotions(int total)
    {
        try
        {
            if (!Player(out var e) || !PotionValues(out _, out int max, out _, out int bonusMax)) return "Unavailable (load a game first)";
            var p = Potions(e);
            total = Math.Clamp(total, 0, max + bonusMax);
            int normal = Math.Min(total, max), bonus = total - normal;
            var curRef = p._currentHealthPotionStat;
            var odRef = p._currentHealthPotionOverdriveStat;
            // Le total d'abord (le jeu ramène le bonus sous « total - max »), puis la part bonus
            if (curRef != null) curRef.SetFloat(total);
            else e.GetComponent<StatsStateComponent>()?.SetFloat(Il2CppType.Of<HealthPotionCurrent>(), total);
            odRef?.SetFloat(bonus);
            GameBridge.RefreshPotionUIPublic(e);
            string diag = "";
            try
            {
                var so = odRef?.GetStatObject();
                if (so != null) diag = $" | bonus : base={so.GetBaseValue()} valeur={so.GetFloat(odRef._e ?? e)} modificateurs={so.GetModifiers()?.Count ?? 0}";
                var co = curRef?.GetStatObject();
                if (co != null) diag += $" | actuelles : base={co.GetBaseValue()} valeur={co.GetFloat(curRef._e ?? e)} modificateurs={co.GetModifiers()?.Count ?? 0}";
            }
            catch (Exception ex) { diag = " | diag : " + ex.Message; }
            Plugin.Logger.LogInfo($"[Potions] demandé {normal} + {bonus} bonus (plafond bonus {bonusMax}) → relu {p.currentHealthPotion} + {p.currentHealthPotionOverdrive}{diag}");
            string msg = bonus > 0 ? $"Potions: {normal} + {bonus} bonus" : $"Potions: {normal} / {max}";
            if (InfinitePotions && normal < max) msg += " (Infinite potions will refill them)";
            return msg;
        }
        catch (Exception ex) { return "Potion change failed: " + ex.Message; }
    }
    public static string ChangePotions(int delta) =>
        PotionValues(out int cur, out _, out int bonus, out _) ? SetPotions(cur + bonus + delta) : "Unavailable (load a game first)";

    /// <summary>
    /// Met à jour les barres de vie et de mana du HUD : le jeu ne les rafraîchit que sur ses propres événements
    /// (dégâts, soin, init…), on leur envoie donc un événement d'initialisation avec les valeurs actuelles.
    /// </summary>
    public static void RefreshBars(Entity e, float? oldMana = null)
    {
        try
        {
            var dmg = e.GetComponent<Damageable>();
            if (dmg != null)
            {
                var init = new EntityInitHealthEvent { e = e, damageable = dmg };
                // Barre du joueur seulement : la barre du boss (HUDBossManager) est aussi un HUDControllerHealth
                // et afficherait sinon les PV du joueur
                foreach (var o in UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<HUDControllerHealth>()))
                {
                    var hud = o.Cast<HUDControllerHealth>();
                    if (hud.hudControllerHealthContext == HUDControllerHealthContext.Player) hud.ProcessInitHealthEvent(init);
                }
            }
            var mana = e.GetComponent<ManaCaster>();
            if (mana != null)
            {
                var type = mana.spellManaType;
                float cur = mana.GetCurrentMana(e, type);
                var init = new ManaInitEvent { e = e, manaType = type, initialValue = cur, maxValue = mana.GetMaxMana(e, type) };
                var change = new ManaChangeEvent { e = e, isPlayer = true, manaCaster = mana, manaType = type, oldValue = oldMana ?? cur, newValue = cur };
                foreach (var o in UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<HUDControllerMana>()))
                {
                    var hud = o.Cast<HUDControllerMana>();
                    hud.ProcessManaInitEvent(init);
                    hud.ProcessManaChangeEvent(change);
                }
            }
            foreach (var o in UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<LifeUIDataReader>()))
                o.Cast<LifeUIDataReader>().ReadLifeDataAndUpdateDisplay();
            foreach (var o in UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<ManaUIDataReader>()))
                o.Cast<ManaUIDataReader>().ReadManaDataAndUpdateDisplay();
        }
        catch (Exception ex) { Plugin.Logger.LogWarning("[Utilitaires] barres : " + ex.Message); }
    }

    static void Warn(string what, Exception ex) =>
        Plugin.Logger.LogWarning($"[Utilitaires] {what} désactivé suite à une erreur : {ex.Message}");
}
