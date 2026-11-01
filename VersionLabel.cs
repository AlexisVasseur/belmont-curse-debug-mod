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

        // Petit encadré au cadre de carte, comme les fenêtres du mod
        const float W = 380f, H = 72f;
        var rt = CardFrame.SmallBox(UiCanvas.Root.transform, "VersionBox", W, H, new Vector2(1f, 1f), out var content);
        rt.anchoredPosition = new Vector2(-18f, -14f);
        box = rt.gameObject;

        var title = Ui.Text(content, "Game", 20, 12, W - 40, 24, GameFonts.Title, 17f, Theme.GoldText);
        title.text = game;
        text = Ui.Text(content, "Mod", 20, 37, W - 40, 24, GameFonts.Body, 17f, Theme.Text);
        text.text = mod;
    }
}
