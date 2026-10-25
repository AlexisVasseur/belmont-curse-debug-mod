using System;
using System.IO;
using System.Linq;
using BepInEx;

namespace BelmontPractice;

/// <summary>Copie des vraies sauvegardes du jeu à chaque lancement (on garde les 10 dernières).</summary>
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

            foreach (var old in new DirectoryInfo(root).GetDirectories().OrderByDescending(d => d.CreationTimeUtc).Skip(Keep))
                old.Delete(true);
            Plugin.Logger.LogInfo($"Sauvegardes du jeu copiées dans {target}");
        }
        catch (Exception ex) { Plugin.Logger.LogWarning("Copie des sauvegardes du jeu impossible : " + ex.Message); }
    }
}
