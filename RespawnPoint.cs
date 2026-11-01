using System;
using MotherBase.EntityFramework;
using UnityEngine;

namespace BelmontPractice;

/// <summary>
/// Point de réapparition choisi (bouton « Set » de l'onglet General) : une savestate prise au moment du clic. Après une
/// mort, une fois que le jeu a fait réapparaître le personnage (salle de sauvegarde, « Réessayer » d'un boss…), cette
/// savestate est rechargée : retour à l'endroit exact, dans l'état du clic.
/// </summary>
public static class RespawnPoint
{
    public static SaveState State { get; private set; }

    static IntPtr lastEntity;
    static int lastHealth = -1;
    static bool died;
    static float aliveSince = -1f;
    // Personnage revenu en jeu depuis ce délai avant le rechargement : la réapparition du jeu est terminée
    const float SettleDelay = 0.5f;

    public static string Set()
    {
        try
        {
            if (!GameBridge.InGame(out string why)) return why;
            var s = GameBridge.Capture(null);
            s.Name = "Respawn point";
            State = s;
            died = false;
            return $"Respawn point set: {RoomLabel.ShortRoom(s.Room ?? "")}";
        }
        catch (Exception ex) { Plugin.Logger.LogError(ex); return "Set respawn failed: " + ex.Message; }
    }

    public static string Clear()
    {
        if (State == null) return "No respawn point set";
        State = null;
        died = false;
        return "Respawn point cleared";
    }

    /// <summary>Texte d'état pour le panneau : salle du point, ou « Not set ».</summary>
    public static string Label => State == null ? "Not set" : RoomLabel.ShortRoom(State.Room ?? "");

    /// <param name="playing">En jeu, hors chargement (du jeu ou du mod).</param>
    /// <param name="load">Chargement d'une savestate par le mod.</param>
    public static void Update(bool playing, Action<SaveState> load)
    {
        if (State == null) { died = false; return; }
        if (!playing) { aliveSince = -1f; return; } // mort suivie d'un chargement : on attend la fin
        try
        {
            if (!MainCharacterSingleton.TryGetEntity(out Entity e) || e == null) { aliveSince = -1f; return; }
            var dmg = e.GetComponent<Damageable>();
            if (dmg == null) return;
            int hp = dmg.currentHealth;
            // Mort : PV tombés à 0 sur le même personnage (pas un personnage tout juste créé, encore à 0)
            if (!died && hp <= 0 && e.Pointer == lastEntity && lastHealth > 0)
            {
                died = true;
                aliveSince = -1f;
                Plugin.Logger.LogInfo("[Réapparition] mort : retour au point choisi après la réapparition du jeu");
            }
            lastEntity = e.Pointer;
            lastHealth = hp;
            if (!died) return;

            if (hp <= 0) { aliveSince = -1f; return; }
            if (aliveSince < 0f) aliveSince = Time.unscaledTime;
            if (Time.unscaledTime - aliveSince < SettleDelay) return;
            died = false;
            aliveSince = -1f;
            Plugin.Logger.LogInfo("[Réapparition] personnage revenu : chargement du point choisi");
            load(State);
        }
        catch (Exception ex) { Plugin.Logger.LogWarning("[Réapparition] " + ex.Message); }
    }
}
