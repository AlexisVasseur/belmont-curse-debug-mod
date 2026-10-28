using System;
using System.IO;
using System.Linq;
using BepInEx;

namespace BelmontPractice;

/// <summary>Copie des vraies sauvegardes du jeu à chaque lancement (on garde les 10 dernières copies horodatées).</summary>
public static class SaveBackup
{
    const int Keep = 10;

    public static void Run()
    {
        try
        {
            string saves = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "AppData", "LocalLow", "KONAMI", "Castlevania Belmont's Curse");
            if (!Directory.Exists(saves)) return;
            var files = Directory.GetFiles(saves, "*.sav");
            if (files.Length == 0) return;

            string root = Path.Combine(Paths.GameRootPath, "BelmontPractice", "SauvegardesJeu");
            string target = Path.Combine(root, DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
            Directory.CreateDirectory(target);
            foreach (var f in files) File.Copy(f, Path.Combine(target, Path.GetFileName(f)), true);

            // Seuls les dossiers horodatés créés ici tournent : un dossier renommé ou ajouté à la main n'est jamais supprimé
            var stamp = new System.Text.RegularExpressions.Regex(@"^\d{4}-\d{2}-\d{2}_\d{2}-\d{2}-\d{2}$");
            foreach (var old in new DirectoryInfo(root).GetDirectories().Where(d => stamp.IsMatch(d.Name))
                         .OrderByDescending(d => d.Name, StringComparer.Ordinal).Skip(Keep))
                old.Delete(true);
            Plugin.Logger.LogInfo($"Sauvegardes du jeu copiées dans {target}");
        }
        catch (Exception ex) { Plugin.Logger.LogWarning("Copie des sauvegardes du jeu impossible : " + ex.Message); }
    }
}
