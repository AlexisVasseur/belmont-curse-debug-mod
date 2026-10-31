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
    public const string Version = "0.48.2";

    /// <summary>Build de BepInEx pour laquelle le mod est compilé (les builds « bleeding edge » changent souvent).</summary>
    public const string BepInExBuild = "6.0.0-be.791";

    internal static ManualLogSource Logger;

    internal static ConfigEntry<bool> ForceSpawnPosition;
    internal static ConfigEntry<float> UiScale;
    internal static ConfigEntry<float> PanelOpacity;
    internal static ConfigEntry<float> NoclipSpeed;
    internal static ConfigEntry<float> CutsceneSpeed;
    internal static ConfigEntry<bool> CutsceneFastForward;
    internal static ConfigEntry<bool> FastLoad;
    internal static ConfigEntry<bool> ReloadRoom;
    internal static ConfigEntry<bool> FadeToBlack;
    internal static ConfigEntry<float> FadeInDuration;
    internal static ConfigEntry<float> FadeOutDuration;
    internal static ConfigEntry<float> BlackHoldDuration;
    internal static ConfigEntry<float> SettleDuration;
    internal static ConfigEntry<bool> PauseDuringFade;
    internal static ConfigEntry<bool> BossDiagnostics;
    internal static ConfigEntry<bool> BossShortIntro;

    public override void Load()
    {
        Logger = Log;

        Keybinds.Init(Config);
        UiScale = Config.Bind("Interface", "Echelle", 0.85f, "Taille de l'interface du mod (1 = grande, 0.5 = moitié)");
        NoclipSpeed = Config.Bind("Utilitaires", "VitesseNoclip", 18f, "Vitesse du noclip (Maj = x3)");
        CutsceneFastForward = Config.Bind("Utilitaires", "AccelerationCinematiques", true, "Raccourci d'accélération des cinématiques (F7 / Y)");
        CutsceneSpeed = Config.Bind("Utilitaires", "VitesseCinematiques", 10f, "Accélération des cinématiques jouées avec les personnages du jeu (2 à 20). Les vidéos ne sont jamais accélérées, seulement passées");
        BossShortIntro = Config.Bind("Utilitaires", "IntroCourteBoss", true, "Fight : le boss arrive avec l'intro courte que le jeu joue après une défaite, au lieu de sa cinématique d'entrée complète");
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
        CheckBepInExBuild();
        SessionLog.Start();
        SaveBackup.Run();
        AddComponent<PracticeBehaviour>();
        Logger.LogInfo($"{Name} {Version} chargé");
    }

    /// <summary>Autre build de BepInEx : le mod peut mal fonctionner, on le signale dans le journal.</summary>
    static void CheckBepInExBuild()
    {
        try
        {
            var info = (System.Reflection.AssemblyInformationalVersionAttribute)System.Attribute.GetCustomAttribute(
                typeof(Paths).Assembly, typeof(System.Reflection.AssemblyInformationalVersionAttribute));
            string running = info?.InformationalVersion ?? "?";
            if (running.StartsWith(BepInExBuild + "+") || running == BepInExBuild) return;
            Logger.LogWarning($"BepInEx {running} détecté : {Name} est prévu pour BepInEx {BepInExBuild} (Unity IL2CPP, Windows x64). "
                + "Avec une autre build, certaines fonctions peuvent ne pas marcher.");
        }
        catch (System.Exception ex) { Logger.LogWarning("Version de BepInEx illisible : " + ex.Message); }
    }
}
