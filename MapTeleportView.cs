using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BelmontPractice;

/// <summary>
/// Affichage de la téléportation par la carte, avec les icônes de touches du jeu :
/// rappel des commandes au-dessus du viseur (carte ouverte) et fenêtre « Teleport to … ? » à côté du viseur.
/// Touches clavier : cadre de touche du jeu avec le nom de la touche ; manette : icônes Start / B du jeu
/// trouvées en mémoire (à défaut, le nom du bouton).
/// </summary>
public static class MapTeleportView
{
    internal const float HintH = 46f, PopW = 430f, PopH = 104f, Glyph = 30f;

    static GameObject hint, popup;
    static RectTransform popupRt, hintRt;
    static float hintW = 300f;
    static TextMeshProUGUI question;
    static string builtForKey;
    static bool builtWithGameFonts, builtWithPadSprites;
    static string pendingQuestion = "";

    // Icônes manette du jeu (sprites chargés en mémoire)
    static Sprite startSprite, eastSprite;
    static float nextPadSearch;
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
            if (mapOpen && (startSprite == null || eastSprite == null) && Time.unscaledTime >= nextPadSearch) { nextPadSearch = Time.unscaledTime + 2f; FindPadSprites(); }
            bool padReady = startSprite != null && eastSprite != null;
            if (hint == null || builtForKey != KeyLabel || (!builtWithGameFonts && GameFonts.Resolved) || (!builtWithPadSprites && padReady)) Build();
            bool showHint = mapOpen && !confirming;
            if (hint.activeSelf != showHint) hint.SetActive(showHint);
            if (showHint) PlaceAboveCursor(cursorCanvas);
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

    /// <summary>Rappel centré juste au-dessus du viseur (en haut de l'écran si la position est inconnue).</summary>
    static void PlaceAboveCursor(Vector2? cursor)
    {
        float W = UiCanvas.Width, H = UiCanvas.Height;
        Vector2 p = cursor ?? new Vector2(W / 2f, H - 140f);
        float x = Mathf.Clamp(p.x - hintW / 2f, 20f, W - hintW - 20f);
        float y = Mathf.Clamp(p.y + 46f, 20f, H - HintH - 20f);
        hintRt.anchoredPosition = new Vector2(x, y);
    }

    static void FindPadSprites()
    {
        try
        {
            var names = new List<string>();
            int startRank = int.MaxValue, eastRank = int.MaxValue;
            string[] start = { "buttonmenu", "menu", "buttonstart", "start", "options" };
            string[] east = { "buttoneast", "east" };
            foreach (var o in Resources.FindObjectsOfTypeAll(Il2CppType.Of<Sprite>()))
            {
                var s = o.TryCast<Sprite>();
                if (s == null) continue;
                string n = s.name ?? "";
                if (!n.StartsWith("xbox_", StringComparison.OrdinalIgnoreCase)) continue;
                names.Add(n);
                string low = n.ToLowerInvariant();
                for (int i = 0; i < start.Length && i < startRank; i++) if (low.Contains(start[i])) { startSprite = s; startRank = i; break; }
                for (int i = 0; i < east.Length && i < eastRank; i++) if (low.Contains(east[i])) { eastSprite = s; eastRank = i; break; }
            }
            if (!glyphsLogged && names.Count > 0)
            {
                glyphsLogged = true;
                Plugin.Logger.LogInfo($"[Carte] icônes manette en mémoire : {string.Join(", ", names)} → Start={startSprite?.name ?? "?"} B={eastSprite?.name ?? "?"}");
            }
        }
        catch (Exception ex) { Plugin.Logger.LogWarning("[Carte] icônes manette : " + ex.Message); }
    }

    /// <summary>Touche clavier : cadre de touche du jeu avec la lettre dedans. Retourne la largeur.</summary>
    internal static float KeyGlyph(Transform parent, float x, float y, string key)
    {
        float w = Mathf.Max(Glyph, 14f + 11f * key.Length);
        var frame = GameSprites.Icon("ui_key_frame", new Vector4(40, 40, 40, 40));
        if (frame != null) Skin.Sliced(parent, "Key" + key, x, y, w, Glyph, frame, Color.white, 4f);
        else Ui.Frame(parent, "Key" + key, x, y, w, Glyph, Theme.GoldBright, Theme.Hex(0x0b0706));
        var t = Ui.Label(parent, "KeyText" + key, x, y, w, Glyph, GameFonts.Title, 18f, Theme.GoldText);
        t.text = key;
        return w;
    }

    /// <summary>Bouton de manette : icône du jeu, sinon son nom dans un cadre de touche. Retourne la largeur.</summary>
    internal static float PadGlyph(Transform parent, float x, float y, Sprite sprite, string fallback)
    {
        if (sprite != null)
        {
            var img = Skin.Sprite(parent, "Pad" + fallback, x, y - 2, Glyph + 4, Glyph + 4, sprite, Color.white);
            img.preserveAspect = true;
            return Glyph + 4;
        }
        return KeyGlyph(parent, x, y, fallback);
    }

    internal static float Txt(Transform parent, float x, float y, string s, Color c, float size = 16f)
    {
        var t = Ui.Text(parent, "T" + s, x, y, 300, Glyph, GameFonts.Title, size, c, TextAlignmentOptions.MidlineLeft);
        t.text = s;
        return t.preferredWidth;
    }

    /// <summary>« [touche] / [bouton manette] » ; retourne la largeur.</summary>
    static float Inputs(Transform parent, float x, float y, string key, Sprite pad, string padName)
    {
        float x0 = x;
        if (key.Length > 0) { x += KeyGlyph(parent, x, y, key) + 6; x += Txt(parent, x, y, "/", Theme.GoldLabel) + 6; }
        x += PadGlyph(parent, x, y, pad, padName);
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
        builtWithPadSprites = startSprite != null && eastSprite != null;
        var root = UiCanvas.Root.transform;

        // Rappel des commandes au-dessus du viseur : « [T] / [Start]  Teleport here »
        // Contenu d'abord (sa largeur fixe celle du cadre), puis fond, liseré et filet bordeaux dessous
        hintRt = Ui.Rect(root, "MapTeleportHint", 0, 0, hintW, HintH);
        hintRt.anchorMin = hintRt.anchorMax = Vector2.zero;
        hintRt.pivot = Vector2.zero;
        hint = hintRt.gameObject;
        float gy = (HintH - Glyph) / 2f;
        float x = 16f + Inputs(hintRt, 16f, gy, KeyLabel, startSprite, "Start") + 12f;
        x += Txt(hintRt, x, gy, "Teleport here", Theme.GoldText, 18f) + 16f;
        hintW = x;
        hintRt.sizeDelta = new Vector2(hintW, HintH);
        var bg = Ui.Image(hintRt, "Fill", 1, 1, hintW - 2, HintH - 2, Theme.Hex(0x140e0c, 0.92f));
        var accent = Ui.Image(hintRt, "Accent", 1, 1, 4, HintH - 2, Theme.Crimson);
        accent.transform.SetAsFirstSibling();
        bg.transform.SetAsFirstSibling();
        Ui.Border(hintRt, 0, 0, hintW, HintH, Theme.Gold);
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
        float rx = 16f + Inputs(pf.transform, 16f, ry, KeyLabel, startSprite, "Start") + 8f;
        rx += Txt(pf.transform, rx, ry, "Go", Theme.Text) + 30f;
        rx += Inputs(pf.transform, rx, ry, "Esc", eastSprite, "B") + 8f;
        Txt(pf.transform, rx, ry, "Cancel", Theme.Text);
        popup.SetActive(false);
    }
}
