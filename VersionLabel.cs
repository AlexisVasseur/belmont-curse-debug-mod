using System;
using TMPro;
using UnityEngine;

namespace BelmontPractice;

/// <summary>
/// Versions du jeu et du mod, en haut à droite de l'écran, tout le temps (menu principal et en jeu) ;
/// à gauche du panneau latéral quand celui-ci est ouvert.
/// </summary>
public static class VersionLabel
{
    static TextMeshProUGUI text;
    static bool builtWithGameFont;
    static float nextFontTry;

    /// <param name="panelOpen">Panneau latéral ouvert : l'encadré se décale à sa gauche pour rester visible.</param>
    public static void Update(bool panelOpen)
    {
        try
        {
            UiCanvas.Ensure();
            // Polices du jeu : recherche au plus une fois par seconde (coûteuse)
            if (!GameFonts.Resolved && Time.unscaledTime >= nextFontTry) { nextFontTry = Time.unscaledTime + 1f; GameFonts.Resolve(); }
            if (text == null || (!builtWithGameFont && GameFonts.Body != null)) Build();
            if (!box.activeSelf) box.SetActive(true);
            var rt = box.GetComponent<RectTransform>();
            var pos = new Vector2(panelOpen ? -(SidePanelView.PanelWidth + 14f) : -18f, -14f);
            if (rt.anchoredPosition != pos) rt.anchoredPosition = pos;
        }
        catch (Exception ex) { Plugin.Logger.LogWarning("[Version] " + ex.Message); }
    }

    static GameObject box;

    static void Build()
    {
        if (box != null) UnityEngine.Object.Destroy(box);
        builtWithGameFont = GameFonts.Body != null;

        string game = $"Castlevania: Belmont's Curse v{Application.version}";
        string mod = $"{Plugin.Name} v{Plugin.Version}";

        // Encadré comme les fenêtres du mod : cadre doré, fond sombre, filet bordeaux à gauche
        const float W = 360f, H = 62f;
        var fill = Ui.Frame(UiCanvas.Root.transform, "VersionBox", 0, 0, W, H, Theme.Gold, Theme.Bg);
        var rt = fill.transform.parent.Cast<RectTransform>();
        rt.anchorMin = new Vector2(1f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-18f, -14f);
        box = rt.gameObject;
        Ui.Image(fill.transform, "Accent", 0, 0, 4, H - 2, Theme.Crimson);

        var title = Ui.Text(fill.transform, "Game", 14, 5, W - 26, 26, GameFonts.Title, 17f, Theme.GoldText);
        title.text = game;
        text = Ui.Text(fill.transform, "Mod", 14, 31, W - 26, 24, GameFonts.Body, 17f, Theme.Text);
        text.text = mod;
    }
}
