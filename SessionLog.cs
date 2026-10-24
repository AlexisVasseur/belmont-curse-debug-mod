using System;
using System.IO;
using System.Linq;
using BepInEx;
using BepInEx.Logging;

namespace BelmontPractice;

/// <summary>
/// Journal BepInEx gardé pour chaque lancement du jeu (BepInEx/journaux_sessions), écrit sans délai :
/// LogOutput.log est écrasé au lancement suivant, ce qui effaçait la trace d'un crash.
/// </summary>
public static class SessionLog
{
    const int Keep = 15;

    public static void Start()
    {
        try
        {
            string dir = Path.Combine(Paths.BepInExRootPath, "journaux_sessions");
            Directory.CreateDirectory(dir);
            foreach (var old in Directory.GetFiles(dir, "session_*.log").OrderByDescending(f => f).Skip(Keep - 1))
                File.Delete(old);
            string name = $"session_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.log";
            BepInEx.Logging.Logger.Listeners.Add(new DiskLogListener(Path.Combine("journaux_sessions", name), LogLevel.All, false, false, 1));
            Plugin.Logger.LogInfo("[Journal] session enregistrée dans " + Path.Combine(dir, name));
        }
        catch (Exception ex) { Plugin.Logger.LogWarning("[Journal] " + ex.Message); }
    }
}
