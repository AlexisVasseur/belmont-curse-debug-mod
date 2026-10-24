using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

namespace BelmontPractice;

/// <summary>
/// Protège la vraie sauvegarde du jeu pendant le chargement d'une savestate.
/// Un rechargement complet (combat de boss, autre niveau) peut faire écrire au jeu sa sauvegarde
/// sur le disque : c'est alors l'état de la savestate qui remplaçait la progression réelle.
/// On garde une copie des fichiers avant le rechargement et on la remet s'ils ont changé.
/// </summary>
public static class SaveGuard
{
    static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "AppData", "LocalLow", "KONAMI", "Castlevania Belmont's Curse");

    static Dictionary<string, byte[]> snapshot;
    static float releaseAt = -1f;

    /// <summary>Avant un rechargement complet : mémorise les fichiers de sauvegarde du jeu.</summary>
    public static void Arm()
    {
        try
        {
            if (!Directory.Exists(Folder)) return;
            snapshot = Directory.GetFiles(Folder, "*.sav").ToDictionary(f => f, File.ReadAllBytes);
            releaseAt = -1f;
            Plugin.Logger.LogInfo($"[Protection] {snapshot.Count} fichier(s) de sauvegarde du jeu mémorisé(s)");
        }
        catch (Exception ex) { snapshot = null; Plugin.Logger.LogWarning("[Protection] " + ex.Message); }
    }

    /// <summary>Fin du chargement : vérifie les fichiers après un court délai (écritures différées du jeu).</summary>
    public static void ReleaseSoon(float delay = 3f)
    {
        if (snapshot != null) releaseAt = Time.unscaledTime + delay;
    }

    /// <summary>Chaque frame.</summary>
    public static void Update()
    {
        if (snapshot == null || releaseAt < 0f || Time.unscaledTime < releaseAt) return;
        try
        {
            foreach (var (path, bytes) in snapshot)
            {
                if (File.Exists(path) && File.ReadAllBytes(path).AsSpan().SequenceEqual(bytes)) continue;
                File.WriteAllBytes(path, bytes);
                Plugin.Logger.LogWarning($"[Protection] {Path.GetFileName(path)} réécrit par le chargement de la savestate : version d'avant remise");
            }
        }
        catch (Exception ex) { Plugin.Logger.LogWarning("[Protection] " + ex.Message); }
        snapshot = null;
        releaseAt = -1f;
    }
}
