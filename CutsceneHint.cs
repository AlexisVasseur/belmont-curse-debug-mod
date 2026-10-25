using System;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace BelmontPractice;

/// <summary>
/// Pendant une cinématique, en bas à droite : « [F7] / [Y] Fast-forward » (icônes de touches du jeu),
/// puis « Fast-forward ×10 » une fois l'accélération lancée.
/// </summary>
public static class CutsceneHint
{
    const float H = MapTeleportView.HintH, Glyph = MapTeleportView.Glyph;

    static GameObject root;
    static RectTransform rt;
    static float width;
    static string builtFor;
    static Sprite padY;
    static float nextPadSearch;

    static string KeyLabel => Keybinds.FastForward.HasKeys ? Keybinds.FastForward.ShortLabel : "";

    public static void Update(bool cutscene, bool armed)
    {
        try
        {
            if (!cutscene && root == null) return;
            UiCanvas.Ensure();
            if (!GameFonts.Resolved) GameFonts.Resolve();
            if (padY == null && Time.unscaledTime >= nextPadSearch) { nextPadSearch = Time.unscaledTime + 2f; FindPadSprite(); }
            string key = $"{KeyLabel}|{armed}|{padY != null}|{GameFonts.Resolved}";
            if (root == null || builtFor != key) Build(armed, key);
            if (root.activeSelf != cutscene)
            {
                root.SetActive(cutscene);
                Plugin.Logger.LogInfo($"[Cinématiques] indication {(cutscene ? $"affichée en {UiCanvas.Width - width - 30f:0},40 (canvas {UiCanvas.Width:0}×{UiCanvas.Height:0})" : "masquée")}");
            }
            if (cutscene) rt.anchoredPosition = new Vector2(UiCanvas.Width - width - 30f, 40f);
        }
        catch (Exception ex) { Plugin.Logger.LogWarning("[Cinématiques] indication : " + ex.Message); }
    }

    static void FindPadSprite()
    {
        foreach (var o in Resources.FindObjectsOfTypeAll(Il2CppType.Of<Sprite>()))
        {
            var s = o.TryCast<Sprite>();
            if (s != null && string.Equals(s.name, "xbox_buttonNorth", StringComparison.OrdinalIgnoreCase)) { padY = s; return; }
        }
    }

    static void Build(bool armed, string key)
    {
        if (root != null) UnityEngine.Object.Destroy(root);
        builtFor = key;
        rt = Ui.Rect(UiCanvas.Root.transform, "CutsceneHint", 0, 0, 300, H);
        rt.anchorMin = rt.anchorMax = Vector2.zero;
        rt.pivot = Vector2.zero;
        root = rt.gameObject;
        float gy = (H - Glyph) / 2f, x = 16f;
        if (!armed)
        {
            if (KeyLabel.Length > 0)
            {
                x += MapTeleportView.KeyGlyph(rt, x, gy, KeyLabel) + 6;
                x += MapTeleportView.Txt(rt, x, gy, "/", Theme.GoldLabel) + 6;
            }
            x += MapTeleportView.PadGlyph(rt, x, gy, padY, "Y") + 12;
        }
        x += MapTeleportView.Txt(rt, x, gy, armed ? "Fast-forward ×10" : "Fast-forward", Theme.GoldText, 18f) + 16f;
        width = x;
        rt.sizeDelta = new Vector2(width, H);
        var bg = Ui.Image(rt, "Fill", 1, 1, width - 2, H - 2, Theme.Hex(0x140e0c, 0.92f));
        var accent = Ui.Image(rt, "Accent", 1, 1, 4, H - 2, Theme.Crimson);
        accent.transform.SetAsFirstSibling();
        bg.transform.SetAsFirstSibling();
        Ui.Border(rt, 0, 0, width, H, Theme.Gold);
        root.SetActive(false);
    }
}
