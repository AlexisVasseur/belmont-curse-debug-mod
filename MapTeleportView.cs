using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BelmontPractice;

/// <summary>
/// Affichage de la téléportation par la carte, avec les icônes de touches du jeu :
/// encadré de rappel en haut à droite (carte ouverte) et fenêtre « Teleport to … ? » à côté du viseur de la carte.
/// Touche clavier : cadre de touche du jeu avec la lettre ; manette : icône Start du jeu trouvée en mémoire
/// (à défaut, le mot « Start »).
/// </summary>
public static class MapTeleportView
{
    const float HintH = 46f, PopW = 430f, PopH = 104f, Glyph = 30f;

    static GameObject hint, popup;
    static RectTransform popupRt;
    static TextMeshProUGUI question;
    static string builtForKey;
    static bool builtWithGameFonts, builtWithStartSprite;
    static string pendingQuestion = "";

    // Icône Start de la manette (sprite du jeu chargé en mémoire)
    static Sprite startSprite;
    static float nextStartSearch;
    static bool glyphsLogged;

    public static void SetQuestion(string q)
    {
        pendingQuestion = q;
        if (question != null) Ui.SetText(question, q);
    }

    static string KeyLabel => Keybinds.MapTeleport.HasKeys ? Keybinds.MapTeleport.ShortLabel : "";

    /// <summary>Chaque frame. cursorCanvas : position du viseur dans le canvas du mod (origine en bas à gauche), si connue.</summary>
    public static void Update(bool mapOpen, bool confirming, Vector2? cursorCanvas)
    {
        try
        {
            if (!mapOpen && hint == null) return;
            UiCanvas.Ensure();
            if (!GameFonts.Resolved) GameFonts.Resolve();
            if (mapOpen && startSprite == null && Time.unscaledTime >= nextStartSearch) { nextStartSearch = Time.unscaledTime + 2f; FindStartSprite(); }
            if (hint == null || builtForKey != KeyLabel || (!builtWithGameFonts && GameFonts.Resolved) || (!builtWithStartSprite && startSprite != null)) Build();
            if (hint.activeSelf != (mapOpen && !confirming)) hint.SetActive(mapOpen && !confirming);
            bool showPop = mapOpen && confirming;
            if (popup.activeSelf != showPop) popup.SetActive(showPop);
            if (showPop) PlaceNearCursor(cursorCanvas);
        }
        catch (Exception ex) { Plugin.Logger.LogWarning("[Carte] affichage : " + ex.Message); }
    }

    /// <summary>À droite du viseur (à gauche s'il est trop près du bord droit), toujours dans l'écran ; sinon au centre.</summary>
    static void PlaceNearCursor(Vector2? cursor)
    {
        float W = UiCanvas.Width, H = UiCanvas.Height;
        Vector2 p = cursor ?? new Vector2(W / 2f - PopW / 2f - 50f, H / 2f);
        bool right = p.x + 50f + PopW < W - 20f;
        float x = right ? p.x + 50f : p.x - 50f - PopW;
        float y = Mathf.Clamp(p.y - PopH / 2f, 20f, H - PopH - 20f);
        x = Mathf.Clamp(x, 20f, W - PopW - 20f);
        popupRt.anchoredPosition = new Vector2(x, y);
    }

    static void FindStartSprite()
    {
        try
        {
            var names = new List<string>();
            Sprite best = null;
            int bestRank = int.MaxValue;
            string[] wanted = { "buttonmenu", "menu", "buttonstart", "start", "options" };
            foreach (var o in Resources.FindObjectsOfTypeAll(Il2CppType.Of<Sprite>()))
            {
                var s = o.TryCast<Sprite>();
                if (s == null) continue;
                string n = s.name ?? "";
                if (!n.StartsWith("xbox_", StringComparison.OrdinalIgnoreCase)) continue;
                names.Add(n);
                string low = n.ToLowerInvariant();
                for (int i = 0; i < wanted.Length && i < bestRank; i++)
                    if (low.Contains(wanted[i])) { best = s; bestRank = i; break; }
            }
            if (!glyphsLogged && names.Count > 0) { glyphsLogged = true; Plugin.Logger.LogInfo("[Carte] icônes manette en mémoire : " + string.Join(", ", names)); }
            if (best != null) { startSprite = best; Plugin.Logger.LogInfo("[Carte] icône Start : " + best.name); }
        }
        catch (Exception ex) { Plugin.Logger.LogWarning("[Carte] icônes manette : " + ex.Message); }
    }

    /// <summary>Touche clavier : cadre de touche du jeu avec la lettre dedans. Retourne la largeur.</summary>
    static float KeyGlyph(Transform parent, float x, float y, string key)
    {
        float w = Mathf.Max(Glyph, 14f + 11f * key.Length);
        var frame = GameSprites.Icon("ui_key_frame", new Vector4(40, 40, 40, 40));
        if (frame != null) Skin.Sliced(parent, "Key" + key, x, y, w, Glyph, frame, Color.white, 4f);
        else Ui.Frame(parent, "Key" + key, x, y, w, Glyph, Theme.GoldBright, Theme.Hex(0x0b0706));
        var t = Ui.Label(parent, "KeyText" + key, x, y, w, Glyph, GameFonts.Title, 18f, Theme.GoldText);
        t.text = key;
        return w;
    }

    /// <summary>Bouton Start de la manette : icône du jeu, sinon le mot. Retourne la largeur.</summary>
    static float StartGlyph(Transform parent, float x, float y)
    {
        if (startSprite != null)
        {
            var img = Skin.Sprite(parent, "StartIcon", x, y - 2, Glyph + 4, Glyph + 4, startSprite, Color.white);
            img.preserveAspect = true;
            return Glyph + 4;
        }
        var t = Ui.Text(parent, "StartText", x, y, 60, Glyph, GameFonts.Title, 17f, Theme.GoldText, TextAlignmentOptions.MidlineLeft);
        t.text = "Start";
        return t.preferredWidth + 2;
    }

    static float Txt(Transform parent, float x, float y, string s, Color c, float size = 16f)
    {
        var t = Ui.Text(parent, "T" + s, x, y, 300, Glyph, GameFonts.Title, size, c, TextAlignmentOptions.MidlineLeft);
        t.text = s;
        return t.preferredWidth;
    }

    /// <summary>« [T] / [Start] » ; retourne la largeur.</summary>
    static float Inputs(Transform parent, float x, float y)
    {
        float x0 = x;
        string k = KeyLabel;
        if (k.Length > 0) { x += KeyGlyph(parent, x, y, k) + 6; x += Txt(parent, x, y, "/", Theme.GoldLabel) + 6; }
        x += StartGlyph(parent, x, y);
        return x - x0;
    }

    static RectTransform Anchor(Image fill, Vector2 anchor, Vector2 pivot)
    {
        var rt = fill.transform.parent.Cast<RectTransform>();
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        return rt;
    }

    static void Build()
    {
        if (hint != null) UnityEngine.Object.Destroy(hint);
        if (popup != null) UnityEngine.Object.Destroy(popup);
        builtForKey = KeyLabel;
        builtWithGameFonts = GameFonts.Resolved;
        builtWithStartSprite = startSprite != null;
        var root = UiCanvas.Root.transform;

        // Rappel des commandes, en haut à droite de la carte : « [T] / [Start]  Teleport here »
        float hintW = 330f;
        var hf = Ui.Frame(root, "MapTeleportHint", 0, 0, hintW, HintH, Theme.Gold, Theme.Hex(0x140e0c, 0.92f));
        var hrt = Anchor(hf, new Vector2(1f, 1f), new Vector2(1f, 1f));
        hrt.anchoredPosition = new Vector2(-48f, -150f);
        hint = hrt.gameObject;
        Ui.Image(hf.transform, "Accent", 0, 0, 4, HintH - 2, Theme.Crimson);
        float x = 16f + Inputs(hf.transform, 16f, (HintH - 2 - Glyph) / 2f) + 12f;
        Txt(hf.transform, x, (HintH - 2 - Glyph) / 2f, "Teleport here", Theme.GoldText, 18f);
        hint.SetActive(false);

        // Fenêtre de confirmation, à côté du viseur
        var pf = Ui.Frame(root, "MapTeleportConfirm", 0, 0, PopW, PopH, Theme.Gold, Theme.Hex(0x140e0c, 0.95f));
        popupRt = Anchor(pf, new Vector2(0f, 0f), new Vector2(0f, 0f));
        popup = popupRt.gameObject;
        Ui.Image(pf.transform, "Accent", 0, 0, 4, PopH - 2, Theme.Crimson);
        question = Ui.Text(pf.transform, "Question", 16, 8, PopW - 30, 40, GameFonts.Title, 20f, Theme.GoldText, TextAlignmentOptions.MidlineLeft);
        question.enableWordWrapping = true;
        question.text = pendingQuestion;
        float ry = PopH - 2 - Glyph - 14;
        float rx = 16f + Inputs(pf.transform, 16f, ry) + 8f;
        rx += Txt(pf.transform, rx, ry, "Go", Theme.Text) + 26f;
        rx += Txt(pf.transform, rx, ry, "Esc / B or move: cancel", Theme.GoldLabel, 15f);
        popup.SetActive(false);
    }
}
