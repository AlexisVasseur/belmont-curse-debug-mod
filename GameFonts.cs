using System;
using System.Collections.Generic;
using System.Text;
using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;

namespace BelmontPractice;

/// <summary>
/// Polices TextMeshPro du jeu, retrouvées parmi celles chargées en mémoire :
/// Bona Nova SC (titres), IM Fell (textes), Pirata One (chiffres).
/// </summary>
public static class GameFonts
{
    public static TMP_FontAsset Title, Body, Numbers;
    public static bool Resolved => Title != null && Body != null && Numbers != null;
    static bool logged;

    public static void Resolve()
    {
        var all = new List<TMP_FontAsset>();
        try
        {
            foreach (var o in Resources.FindObjectsOfTypeAll(Il2CppType.Of<TMP_FontAsset>()))
            {
                var f = o.TryCast<TMP_FontAsset>();
                if (f != null) all.Add(f);
            }
        }
        catch (Exception ex) { Plugin.Logger.LogWarning("[Polices] " + ex.Message); }

        var sb = new StringBuilder();
        TMP_FontAsset Find(params string[] keys)
        {
            foreach (var key in keys)
                foreach (var f in all)
                    if (Describe(f).IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0) return f;
            return null;
        }

        foreach (var f in all) sb.Append("\n  - ").Append(Describe(f));
        Title ??= Find("Bona Nova", "BonaNova", "LatinFont", "BaseFont");
        Body ??= Find("Fell DW Pica", "FellDWPica", "DW Pica", "IM Fell", "LatinFont", "BaseFont");
        // Chiffres du jeu (« 200/200 », « Niv 99 ») : NumberFont = Pirata One
        Numbers ??= Find("NumberFont", "Pirata One") ?? Title;

        if (!logged && all.Count > 0)
        {
            logged = true;
            Plugin.Logger.LogInfo($"[Polices] {all.Count} polices TMP trouvées :{sb}");
            Plugin.Logger.LogInfo($"[Polices] titres={Title?.name ?? "défaut"}, textes={Body?.name ?? "défaut"}, chiffres={Numbers?.name ?? "défaut"}");
        }
    }

    static string Describe(TMP_FontAsset f)
    {
        string family = "";
        string source = "";
        try { family = f.faceInfo.familyName ?? ""; } catch { }
        try { source = f.sourceFontFile != null ? f.sourceFontFile.name : ""; } catch { }
        return $"{f.name} [{family}] ({source})";
    }
}
