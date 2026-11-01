using System;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;

namespace BelmontPractice;

/// <summary>
/// Nom de la salle en bas à droite de l'écran, en jeu (option « Room name ») : la zone du jeu en titre, l'identifiant
/// de la salle dessous (celui des savestates et des arènes). Masqué pendant les chargements longs (jeu, savestates), il
/// réapparaît en fondu quand le joueur reprend la main ; les passages d'une salle à l'autre ne le masquent pas.
/// </summary>
public static class RoomLabel
{
    static GameObject box;
    static TextMeshProUGUI area, room;
    static bool builtWithGameFont;
    static float nextRefresh, controlSince = -1f;
    static string shownLevel, shownRoom;
    static readonly Fade fade = new(0.4f, 0f);
    // Main rendue depuis ce délai avant de réapparaître : pas de clignotement entre deux transitions rapprochées
    const float ControlDelay = 0.3f;

    /// <param name="playing">En jeu, hors chargement long.</param>
    /// <param name="panelOpen">Panneau latéral ouvert : l'encadré se décale à sa gauche pour rester visible.</param>
    public static void Update(bool playing, bool panelOpen)
    {
        try
        {
            bool show = playing && Plugin.ShowRoomName.Value;
            if (!show) { fade.Out(box); controlSince = -1f; return; }
            if (controlSince < 0f) controlSince = Time.unscaledTime;
            if (Time.unscaledTime - controlSince < ControlDelay) return;
            if (box == null || (!builtWithGameFont && GameFonts.Body != null)) Build();
            fade.In(box);
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
            room.text = id.Length > 0 ? ShortRoom(id) : "—";
        }
        catch (Exception ex) { Plugin.Logger.LogWarning("[Salle] affichage : " + ex.Message); }
    }

    /// <summary>
    /// Identifiant raccourci : « World_Biome_01_Streets_Poor_StreetsPoorV2_Room17_uid101 » → « StreetsPoorV2 - Room17 » (le
    /// point médian n'existe pas dans la police du jeu). Sans « RoomNN » : préfixe World_Biome_NN_ et suffixe _uidNN retirés.
    /// </summary>
    public static string ShortRoom(string id)
    {
        string s = Regex.Replace(id, @"_uid\d+$", "");
        var parts = s.Split('_');
        int r = Array.FindLastIndex(parts, p => Regex.IsMatch(p, @"^Room\d+$"));
        if (r > 0) return parts[r - 1] + " - " + parts[r];
        if (r == 0) return parts[0];
        s = Regex.Replace(s, @"^World_Biome_\d+_", "").Replace('_', ' ');
        return s.Length > 0 ? s : id;
    }

    static void Build()
    {
        if (box != null) UnityEngine.Object.Destroy(box);
        builtWithGameFont = GameFonts.Body != null;
        shownLevel = shownRoom = null;

        // Même encadré que les versions (en haut à droite) : cadre de carte réduit
        const float W = 380f, H = 72f;
        var rt = CardFrame.SmallBox(UiCanvas.Root.transform, "RoomBox", W, H, new Vector2(1f, 0f), out var content);
        rt.anchoredPosition = new Vector2(-18f, 14f);
        box = rt.gameObject;

        area = Ui.Text(content, "Area", 20, 12, W - 40, 24, GameFonts.Title, 17f, Theme.GoldText);
        room = Ui.Text(content, "Room", 20, 37, W - 40, 24, GameFonts.Body, 17f, Theme.Text);
        room.enableAutoSizing = true; // identifiant trop long : rapetisse au lieu de déborder
        room.fontSizeMin = 11f;
        room.fontSizeMax = 17f;
    }
}
