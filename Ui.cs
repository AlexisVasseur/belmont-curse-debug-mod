using System;
using Il2CppInterop.Runtime;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BelmontPractice;

/// <summary>Palette inspirée de l'interface du jeu (cadres dorés, bordeaux, fond brun-noir).</summary>
public static class Theme
{
    // Fonds semi-transparents (réglage Interface/Opacite) : on voit le jeu derrière, comme avec DebugMod.
    static float Opacity => Mathf.Clamp01(Plugin.PanelOpacity?.Value ?? 0.85f);
    public static Color Bg => Hex(0x140e0c, Opacity);
    public static Color BgButton => Hex(0x1c1411, Mathf.Min(1f, Opacity + 0.08f));
    public static Color BgButtonHover => Hex(0x3a2418, Mathf.Min(1f, Opacity + 0.1f));
    public static Color BgGap => Hex(0x0b0706, Opacity);
    public static readonly Color BgSolid = Hex(0x140e0c); // intérieur des losanges (posés sur une bordure pleine)
    public static Color TileOn => Hex(0x33241a, Mathf.Min(1f, Opacity + 0.08f));
    public static Color TileOff => Hex(0x120c0a, Opacity);
    public static readonly Color Gold = Hex(0x8a6d3b);
    public static readonly Color GoldBright = Hex(0xa8864a);
    public static readonly Color GoldText = Hex(0xe2c27e);
    public static readonly Color GoldLabel = Hex(0xc9a865);
    public static readonly Color KeyText = Hex(0xe8cf95);
    public static readonly Color Text = Hex(0xe6d3b0);
    public static readonly Color TextSelected = Hex(0xf2e2c0);
    public static readonly Color TextDim = Hex(0x6b5640);
    public static readonly Color Crimson = Hex(0x5e1418);
    public static readonly Color CrimsonDark = Hex(0x3a0d10);
    public static readonly Color Red = Hex(0xd9534f);
    public static readonly Color Line = Hex(0x2e2019);
    public static readonly Color LineGold = Hex(0x5a3a22);
    public static readonly Color OuterLine = Hex(0x3a2418);

    // Comparaison champ par champ (les opérateurs de Color peuvent être absents du jeu compilé).
    public static bool Same(Color a, Color b) => a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;

    public static Color Hex(int rgb, float a = 1f) =>
        new(((rgb >> 16) & 0xff) / 255f, ((rgb >> 8) & 0xff) / 255f, (rgb & 0xff) / 255f, a);
}

/// <summary>Petites fonctions de construction uGUI, en coordonnées « haut-gauche » (pixels de la résolution de référence).</summary>
public static class Ui
{
    public static RectTransform Rect(Transform parent, string name, float x, float y, float w, float h)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        Place(rt, x, y, w, h);
        return rt;
    }

    public static void Place(RectTransform rt, float x, float y, float w, float h)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(x, -y);
        rt.sizeDelta = new Vector2(w, h);
    }

    public static Image Image(Transform parent, string name, float x, float y, float w, float h, Color color)
    {
        var rt = Rect(parent, name, x, y, w, h);
        var img = rt.gameObject.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        return img;
    }

    /// <summary>
    /// Rectangle à bordure fine : renvoie le fond intérieur (son parent est le conteneur du cadre).
    /// La bordure est faite de 4 filets (pas d'un rectangle plein dessous), pour que les fonds
    /// semi-transparents laissent bien voir le jeu.
    /// </summary>
    public static Image Frame(Transform parent, string name, float x, float y, float w, float h, Color border, Color fill, float thickness = 1f)
    {
        var outer = Rect(parent, name, x, y, w, h);
        Border(outer, 0, 0, w, h, border, thickness);
        return Image(outer, "Fill", thickness, thickness, w - 2 * thickness, h - 2 * thickness, fill);
    }

    /// <summary>Quatre filets formant un cadre vide (enfants 0 à 3 du parent s'il est créé en premier).</summary>
    public static void Border(Transform parent, float x, float y, float w, float h, Color color, float t = 1f)
    {
        Image(parent, "EdgeTop", x, y, w, t, color);
        Image(parent, "EdgeBottom", x, y + h - t, w, t, color);
        Image(parent, "EdgeLeft", x, y + t, t, h - 2 * t, color);
        Image(parent, "EdgeRight", x + w - t, y + t, t, h - 2 * t, color);
    }

    /// <summary>Change la couleur de la bordure d'un cadre créé par Frame (à partir de son fond).</summary>
    public static void SetBorder(Image frameFill, Color color)
    {
        var outer = frameFill.transform.parent;
        for (int i = 0; i < 4 && i < outer.childCount; i++)
        {
            var img = outer.GetChild(i).GetComponent<Image>();
            if (img != null && !Theme.Same(img.color, color)) img.color = color;
        }
    }

    public static TextMeshProUGUI Text(Transform parent, string name, float x, float y, float w, float h,
        TMP_FontAsset font, float size, Color color, TextAlignmentOptions align = TextAlignmentOptions.MidlineLeft)
    {
        var rt = Rect(parent, name, x, y, w, h);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) t.font = font;
        t.fontSize = size;
        t.color = color;
        t.alignment = align;
        t.enableWordWrapping = false;
        t.overflowMode = TextOverflowModes.Ellipsis;
        t.raycastTarget = false;
        t.richText = false;
        return t;
    }

    /// <summary>
    /// Petit texte centré sur la forme réelle des caractères (chiffres dans un losange, touche dans une pastille).
    /// Débordement autorisé : en mode « … », TMP n'affiche rien si la ligne est plus haute que la case.
    /// </summary>
    public static TextMeshProUGUI Label(Transform parent, string name, float x, float y, float w, float h,
        TMP_FontAsset font, float size, Color color)
    {
        var t = Text(parent, name, x, y, w, h, font, size, color, TextAlignmentOptions.MidlineGeoAligned);
        t.overflowMode = TextOverflowModes.Overflow;
        return t;
    }

    /// <summary>Losange (carré tourné de 45°) à bordure dorée, avec un texte centré.</summary>
    public static (Image border, Image fill, TextMeshProUGUI label) Diamond(Transform parent, string name, float cx, float cy, float size,
        TMP_FontAsset font, float fontSize)
    {
        var holder = Rect(parent, name, cx - size / 2f, cy - size / 2f, size, size);
        var border = Image(holder, "Border", 0, 0, size, size, Theme.GoldBright);
        border.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
        border.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        border.rectTransform.anchoredPosition = new Vector2(size / 2f, -size / 2f);
        var fill = Image(border.transform, "Fill", 1.5f, 1.5f, size - 3f, size - 3f, Theme.BgSolid);
        var label = Label(holder, "Label", -4, 0, size + 8, size, font, fontSize, Theme.KeyText);
        return (border, fill, label);
    }

    public static void SetText(TextMeshProUGUI t, string value)
    {
        if (t != null && t.text != value) t.text = value;
    }

    public static void SetActive(Component c, bool active)
    {
        if (c != null && c.gameObject.activeSelf != active) c.gameObject.SetActive(active);
    }
}

/// <summary>Bouton dessiné à la main : la détection du clic est faite par le panneau (pas d'EventSystem).</summary>
public class UiButton
{
    public RectTransform Rt;
    public Image Fill;
    public TextMeshProUGUI Label;
    public Action OnClick;
    public bool Enabled = true;
    public bool Visible => Rt != null && Rt.gameObject.activeInHierarchy;

    public static UiButton Create(Transform parent, string name, float x, float y, float w, float h, string text, TMP_FontAsset font, float fontSize, Action onClick)
    {
        var fill = Ui.Frame(parent, name, x, y, w, h, Theme.Gold, Theme.BgButton);
        var label = Ui.Text(fill.transform, "Label", 0, 0, w - 2, h - 2, font, fontSize, Theme.KeyText, TextAlignmentOptions.Center);
        label.text = text;
        return new UiButton { Rt = fill.transform.parent.Cast<RectTransform>(), Fill = fill, Label = label, OnClick = onClick };
    }

    /// <summary>Couleurs « au repos », fixées par la vue (ex. Edit actif en bordeaux, Read en rouge pendant une opération).</summary>
    public Color BaseColor = Theme.BgButton;
    public Color LabelColor = Theme.KeyText;

    public void SetHover(bool hover)
    {
        var c = Enabled && hover ? Theme.BgButtonHover : BaseColor;
        if (!Theme.Same(Fill.color, c)) Fill.color = c;
        var tc = Enabled ? LabelColor : Theme.TextDim;
        if (!Theme.Same(Label.color, tc)) Label.color = tc;
    }
}
