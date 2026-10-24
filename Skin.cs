using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace BelmontPractice;

/// <summary>
/// Habillage du panneau latéral avec les assets d'interface du jeu (dossier icons/, préfixe ui_) :
/// fond et volutes des boutons, ruban des titres, cadres d'emplacement, flèches d'équipement, losanges, gemmes.
/// Si un fichier manque, chaque élément retombe sur le style à filets dorés.
/// </summary>
public static class Skin
{
    public static Sprite Button => GameSprites.Icon("ui_button", new Vector4(25, 0, 25, 0));
    public static Sprite Ornament => GameSprites.Icon("ui_button_ornament");
    public static Sprite BannerSprite => GameSprites.Icon("ui_banner", new Vector4(240, 0, 240, 0));
    public static Sprite Slot => GameSprites.Icon("ui_slot");
    public static Sprite Equipped => GameSprites.Icon("ui_equipped");
    public static Sprite Diamond => GameSprites.Icon("ui_diamond");
    public static Sprite DiamondHover => GameSprites.Icon("ui_diamond_hover");
    public static Sprite GemLocked => GameSprites.Icon("ui_gem_locked");
    public static Sprite GemUnlocked => GameSprites.Icon("ui_gem_unlocked");
    public static Sprite GemHover => GameSprites.Icon("ui_gem_hover");
    public static Sprite Glow => GameSprites.Icon("ui_glow");

    // Couleurs des boutons habillés
    public static readonly Color ButtonIdle = Theme.Hex(0x3b2a1f);
    public static readonly Color ButtonHover = Theme.Hex(0x050302);
    public static readonly Color TabIdle = Theme.Hex(0x241914);
    public static readonly Color ButtonText = Theme.Hex(0xf4dfa8);
    public static readonly Color Flame = Theme.Hex(0xff7a1a);

    /// <summary>Image découpée en 9 : multiplier = facteur de réduction des bords fixes (bord affiché = bord source / multiplier).</summary>
    public static Image Sliced(Transform parent, string name, float x, float y, float w, float h, Sprite sprite, Color color, float multiplier)
    {
        var img = Ui.Image(parent, name, x, y, w, h, color);
        img.sprite = sprite;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = multiplier;
        return img;
    }

    public static Image Sprite(Transform parent, string name, float x, float y, float w, float h, Sprite sprite, Color color)
    {
        var img = Ui.Image(parent, name, x, y, w, h, color);
        img.sprite = sprite;
        return img;
    }

    /// <summary>
    /// Volutes dorées de part et d'autre d'un bouton (image du bouton survolé du jeu), coupées aux côtés
    /// par un masque pour ne garder que la boucle, sans les tiges qui débordent sur les voisins.
    /// </summary>
    public static (GameObject left, GameObject right) Ornaments(Transform parent, float w, float h)
    {
        var sprite = Ornament;
        if (sprite == null) return (null, null);
        float oh = h + 6f, ow = oh * sprite.rect.width / sprite.rect.height, visible = Mathf.Min(ow, Mathf.Max(12f, h * 0.5f));

        var left = Ui.Rect(parent, "OrnamentL", -3f, -3f, visible, oh);
        left.gameObject.AddComponent<RectMask2D>();
        Sprite(left, "Image", 0f, 0f, ow, oh, sprite, Color.white);

        var right = Ui.Rect(parent, "OrnamentR", w + 3f - visible, -3f, visible, oh);
        right.gameObject.AddComponent<RectMask2D>();
        var r = Sprite(right, "Image", visible, 0f, ow, oh, sprite, Color.white);
        r.rectTransform.localScale = new Vector3(-1f, 1f, 1f); // miroir : la boucle reste du côté extérieur

        left.gameObject.SetActive(false);
        right.gameObject.SetActive(false);
        return (left.gameObject, right.gameObject);
    }

    /// <summary>Titre de section posé sur le ruban bordeaux de l'Armurerie.</summary>
    public static float Banner(Transform parent, string text, float x, float y, float w, float h, TMP_FontAsset font)
    {
        Sliced(parent, "Banner" + text, x, y, w, h, BannerSprite, Color.white, 4f);
        var t = Ui.Text(parent, "BannerText" + text, x, y, w, h, font, 25f, Theme.GoldText, TextAlignmentOptions.Center);
        t.text = text;
        return y + h;
    }
}
