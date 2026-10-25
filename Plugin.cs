using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;

namespace BelmontPractice;

[BepInPlugin(Guid, Name, Version)]
public class Plugin : BasePlugin
{
    public const string Guid = "fr.vasseur.belmontpractice";
    public const string Name = "Belmont Practice";
    public const string Version = "0.28.1";

    internal static ManualLogSource Logger;

    internal static ConfigEntry<bool> ForceSpawnPosition;
    internal static ConfigEntry<float> UiScale;
    internal static ConfigEntry<float> PanelOpacity;
    internal static ConfigEntry<float> NoclipSpeed;
    internal static ConfigEntry<bool> FastLoad;
    internal static ConfigEntry<bool> ReloadRoom;
    internal static ConfigEntry<bool> FadeToBlack;
    internal static ConfigEntry<float> FadeInDuration;
    internal static ConfigEntry<float> FadeOutDuration;
    internal static ConfigEntry<float> BlackHoldDuration;
    internal static ConfigEntry<float> SettleDuration;
    internal static ConfigEntry<bool> PauseDuringFade;
    internal static ConfigEntry<bool> BossDiagnostics;

    public override void Load()
    {
        Logger = Log;

        Keybinds.Init(Config);
        UiScale = Config.Bind("Interface", "Echelle", 0.85f, "Taille de l'interface du mod (1 = grande, 0.5 = moitié)");
        NoclipSpeed = Config.Bind("Utilitaires", "VitesseNoclip", 18f, "Vitesse du noclip (Maj = x3)");
        PanelOpacity = Config.Bind("Interface", "Opacite", 0.85f, "Opacité du fond des fenêtres du mod (1 = opaque, 0.5 = très transparent)");
        FastLoad = Config.Bind("Chargement", "Rapide", true, "Dans le même niveau/biome : restaure sur place sans écran de chargement");
        ReloadRoom = Config.Bind("Chargement", "RechargerSalle", true, "Chargement rapide : force le rechargement de la salle (ennemis remis à zéro)");
        FadeToBlack = Config.Bind("Chargement", "FonduNoir", true, "Fondu au noir complet pendant tout le chargement (tous les cas)");
        FadeInDuration = Config.Bind("Fondu", "EntreeSecondes", 0.05f, "Durée du fondu vers le noir");
        BlackHoldDuration = Config.Bind("Fondu", "NoirMinimumSecondes", 0.1f, "Durée minimale d'écran noir, même si la salle se recharge plus vite");
        SettleDuration = Config.Bind("Fondu", "CalmeRequisSecondes", 0.1f, "Le jeu doit être inactif (aucun chargement / transition / mouvement caméra) pendant cette durée avant de rendre l'image");
        PauseDuringFade = Config.Bind("Fondu", "PausependantFondu", true, "Met le jeu en pause pendant le passage au noir et le retour de l'image");
        FadeOutDuration = Config.Bind("Fondu", "SortieSecondes", 0.15f, "Durée du retour depuis le noir");
        BossDiagnostics = Config.Bind("Debug", "RelevesBoss", false, "Écrit dans le journal le scénario, les acteurs et les spawners des arènes de boss (pour diagnostiquer un boss qui ne revient pas)");
        ForceSpawnPosition = Config.Bind("Debug", "ForcerPosition", true, "Au chargement complet, force la position du joueur via MainCharacterSpawnLogic.SetForcePosition");

        // Pas de hooks Harmony : en IL2CPP, les méthodes au code identique (ex. méthodes vides) partagent
        // le même code natif, et patcher l'une patche des milliers d'autres méthodes du jeu.
        SessionLog.Start();
        SaveBackup.Run();
        AddComponent<PracticeBehaviour>();
        Logger.LogInfo($"{Name} {Version} chargé");
    }
}
