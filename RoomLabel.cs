using System;
using TMPro;
using UnityEngine;

namespace BelmontPractice;

/// <summary>
/// Nom de la salle en bas à droite de l'écran, en jeu (option « Room name ») : la zone du jeu en titre, l'identifiant
/// de la salle dessous (celui des savestates et des arènes).
/// </summary>
public static class RoomLabel
{
    static GameObject box;
    static TextMeshProUGUI area, room;
    static bool builtWithGameFont;
    static float nextRefresh;
    static string shownLevel, shownRoom;

    /// <param name="panelOpen">Panneau latéral ouvert : l'encadré se décale à sa gauche pour rester visible.</param>
    public static void Update(bool inGame, bool panelOpen)
    {
        try
        {
            bool show = inGame && Plugin.ShowRoomName.Value;
            if (!show) { if (box != null && box.activeSelf) box.SetActive(false); return; }
            if (box == null || (!builtWithGameFont && GameFonts.Body != null)) Build();
            if (!box.activeSelf) box.SetActive(true);
            var rt = box.GetComponent<RectTransform>();
            var pos = new Vector2(panelOpen ? -(SidePanelView.PanelWidth + 14f) : -18f, 14f);
            if (rt.anchoredPosition != pos) rt.anchoredPosition = pos;
            if (Time.unscaledTime < nextRefresh) return;
            nextRefresh = Time.unscaledTime + 0.25f;
            string level = GameBridge.CurrentLevelName() ?? "", id = GameBridge.CurrentRoomId() ?? "";
            if (level == shownLevel && id == shownRoom) return;
            shownLevel = level;
            shownRoom = id;
            area.text = level.Length > 0 ? Arenas.AreaLabel(level) : "—";
            room.text = id.Length > 0 ? id : "—";
        }
        catch (Exception ex) { Plugin.Logger.LogWarning("[Salle] affichage : " + ex.Message); }
    }

    static void Build()
    {
        if (box != null) UnityEngine.Object.Destroy(box);
        builtWithGameFont = GameFonts.Body != null;
        shownLevel = shownRoom = null;

        // Même encadré que les versions (en haut à droite) : cadre doré, fond sombre, filet bordeaux à gauche
        const float W = 440f, H = 62f; // plus large que les versions : les identifiants de salle sont longs
        var fill = Ui.Frame(UiCanvas.Root.transform, "RoomBox", 0, 0, W, H, Theme.Gold, Theme.Bg);
        var rt = fill.transform.parent.Cast<RectTransform>();
        rt.anchorMin = new Vector2(1f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(1f, 0f);
        rt.anchoredPosition = new Vector2(-18f, 14f);
        box = rt.gameObject;
        Ui.Image(fill.transform, "Accent", 0, 0, 4, H - 2, Theme.Crimson);

        area = Ui.Text(fill.transform, "Area", 14, 5, W - 26, 26, GameFonts.Title, 17f, Theme.GoldText);
        room = Ui.Text(fill.transform, "Room", 14, 31, W - 26, 24, GameFonts.Body, 17f, Theme.Text);
        room.enableAutoSizing = true; // identifiant trop long : rapetisse au lieu de déborder
        room.fontSizeMin = 11f;
        room.fontSizeMax = 17f;
    }
}
