using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BelmontPractice;

/// <summary>
/// Affichage de la téléportation par la carte : encadré de rappel des commandes (carte ouverte)
/// et fenêtre de confirmation « Teleport to … ? » au centre de l'écran, dans le style du mod.
/// </summary>
public static class MapTeleportView
{
    const float HintW = 360f, HintH = 46f, PopW = 600f, PopH = 176f;

    static GameObject hint, popup;
    static TextMeshProUGUI hintText, question, footer;
    static string builtForKey;
    static bool builtWithGameFonts;
    static string pendingQuestion = "";

    public static void SetQuestion(string q)
    {
        pendingQuestion = q;
        if (question != null) Ui.SetText(question, q);
    }

    static string Keys => (Keybinds.MapTeleport.HasKeys ? Keybinds.MapTeleport.ShortLabel + " / " : "") + "Start";

    public static void Update(bool mapOpen, bool confirming)
    {
        try
        {
            if (!mapOpen && hint == null) return;
            UiCanvas.Ensure();
            if (!GameFonts.Resolved) GameFonts.Resolve();
            if (hint == null || builtForKey != Keys || (!builtWithGameFonts && GameFonts.Resolved)) Build();
            if (hint.activeSelf != (mapOpen && !confirming)) hint.SetActive(mapOpen && !confirming);
            if (popup.activeSelf != (mapOpen && confirming)) popup.SetActive(mapOpen && confirming);
        }
        catch (Exception ex) { Plugin.Logger.LogWarning("[Carte] affichage : " + ex.Message); }
    }

    static RectTransform Anchor(Image fill, Vector2 anchor, Vector2 pivot, Vector2 pos)
    {
        var rt = fill.transform.parent.Cast<RectTransform>();
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        return rt;
    }

    static void Build()
    {
        if (hint != null) UnityEngine.Object.Destroy(hint);
        if (popup != null) UnityEngine.Object.Destroy(popup);
        builtForKey = Keys;
        builtWithGameFonts = GameFonts.Resolved;
        var root = UiCanvas.Root.transform;

        // Rappel des commandes, en haut à droite de la carte
        var hf = Ui.Frame(root, "MapTeleportHint", 0, 0, HintW, HintH, Theme.Gold, Theme.Hex(0x140e0c, 0.92f));
        hint = Anchor(hf, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-48f, -150f)).gameObject;
        Ui.Image(hf.transform, "Accent", 0, 0, 4, HintH - 2, Theme.Crimson);
        hintText = Ui.Text(hf.transform, "Text", 16, 0, HintW - 28, HintH - 2, GameFonts.Title, 18f, Theme.GoldText, TextAlignmentOptions.MidlineLeft);
        hintText.text = $"{Keys}   Teleport here";
        hint.SetActive(false);

        // Fenêtre de confirmation, au centre
        var pf = Ui.Frame(root, "MapTeleportConfirm", 0, 0, PopW, PopH, Theme.Gold, Theme.Hex(0x140e0c, 0.95f));
        popup = Anchor(pf, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero).gameObject;
        if (Skin.BannerSprite != null) Skin.Banner(pf.transform, "Teleport", 0, 0, PopW - 2, 58, GameFonts.Title);
        else Ui.Text(pf.transform, "Title", 0, 6, PopW - 2, 40, GameFonts.Title, 26f, Theme.GoldText, TextAlignmentOptions.Center).text = "Teleport";
        question = Ui.Text(pf.transform, "Question", 20, 66, PopW - 42, 50, GameFonts.Body, 23f, Theme.Text, TextAlignmentOptions.Center);
        question.enableWordWrapping = true;
        question.text = pendingQuestion;
        footer = Ui.Text(pf.transform, "Footer", 20, PopH - 52, PopW - 42, 36, GameFonts.Title, 16f, Theme.GoldLabel, TextAlignmentOptions.Center);
        footer.text = $"{Keys}: go        Esc / B or move the cursor: cancel";
        popup.SetActive(false);
    }
}
