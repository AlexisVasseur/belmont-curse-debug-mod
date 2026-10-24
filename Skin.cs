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
    public static Sprite BannerSprite => GameSprites.Icon("ui_banner_plain"); // ruban seul, sans les ornements des extrémités
    public static Sprite Slot => GameSprites.Icon("ui_slot");
    public static Sprite Equipped => GameSprites.Icon("ui_equipped");
    public static Sprite Diamond => GameSprites.Icon("ui_diamond");
    public static Sprite DiamondHover => GameSprites.Icon("ui_diamond_hover");
    public static Sprite GemLocked => GameSprites.Icon("ui_gem_locked");
    public static Sprite GemUnlocked => GameSprites.Icon("ui_gem_unlocked");
    public static Sprite GemHover => GameSprites.Icon("ui_gem_hover");
    public static Sprite Glow => GameSprites.Icon("ui_glow");
    // Lueurs qui épousent la forme (bord du carré / du losange à 24..104 sur 128 px), à teinter
    public static Sprite GlowSquare => GameSprites.Icon("ui_glow_square");
    public static Sprite GlowDiamond => GameSprites.Icon("ui_glow_diamond");
    public const float GlowScale = 128f / 80f;
    public static Sprite Circle => GameSprites.Icon("ui_circle");
    public static Sprite Disc => GameSprites.Icon("ui_disc");
    /// <summary>Cadre des notifications du jeu (rempli après l'extraction des assets ; null tant qu'il manque).</summary>
    public static Sprite Notification => GameSprites.Icon("ui_notification", new Vector4(0, 60, 0, 60));
    public static Sprite NotificationGold => GameSprites.Icon("ui_notification_gold");
    // Fioles du HUD : pleine, bonus, vide, et l'ornement qui sépare les normales des bonus
    public static Sprite Potion => GameSprites.Icon("ui_potion");
    // Cœur du menu Progression en couches : fond, 4 pièces (vie ou mana), cadre
    public static Sprite HeartBg => GameSprites.Icon("ui_heart_bg");
    public static Sprite HeartFrame => GameSprites.Icon("ui_heart_frame");
    public static Sprite HeartPiece(bool mana, int i) => GameSprites.Icon($"ui_heart_{(mana ? "mana" : "life")}_{i}");
    public static Sprite PotionBonus => GameSprites.Icon("ui_potion_bonus");
    public static Sprite PotionEmpty => GameSprites.Icon("ui_potion_empty");
    public static Sprite PotionSeparator => GameSprites.Icon("ui_potion_separator");
    public static Sprite RowSupport => GameSprites.Icon("ui_row_support", new Vector4(500, 0, 500, 0));
    /// <summary>Icône de famille d'armes (médaillon de l'Armurerie), ex. « WeaponSpear » → ui_family_spear.</summary>
    public static Sprite Family(string key) => GameSprites.Icon("ui_family_" + key.Replace("Weapon", "").ToLowerInvariant());
    /// <summary>Teinte du cadre selon le rang (1 à 4), comme les bordures de l'Armurerie.</summary>
    public static Color TierTint(int tier) => tier switch
    {
        1 => Theme.Hex(0xc8a777),
        2 => Theme.Hex(0xc98c74),
        3 => Theme.Hex(0x8fb6d6),
        _ => Theme.Hex(0xe8c35e),
    };

    // Couleurs des boutons habillés
    public static readonly Color ButtonIdle = Theme.Hex(0x3b2a1f);
    public static readonly Color ButtonHover = Theme.Hex(0x050302);
    public static readonly Color TabIdle = Theme.Hex(0x241914);
    public static readonly Color ButtonText = Theme.Hex(0xf4dfa8);
    public static readonly Color Flame = Theme.Hex(0xff7a1a);
    // États communs à toutes les cases : orange au survol, bleu quand l'objet est équipé
    public static readonly Color HoverGlow = Theme.Hex(0xff8a2a);
    public static readonly Color EquippedGlow = Theme.Hex(0x4aa8ff);

    /// <summary>Lueur en forme, centrée sur un élément de taille « size » (carré ou losange).</summary>
    public static Image ShapeGlow(Transform parent, string name, float cx, float cy, float size, Sprite sprite)
    {
        float g = size * GlowScale;
        var img = Sprite(parent, name, cx - g / 2f, cy - g / 2f, g, g, sprite, Color.white);
        img.gameObject.SetActive(false);
        return img;
    }

    /// <summary>Survol : orange qui pulse ; équipé : bleu fixe ; sinon éteint.</summary>
    public static void ApplyGlow(Image glow, bool hover, bool equipped)
    {
        if (glow == null) return;
        Ui.SetActive(glow, hover || equipped);
        if (hover) glow.color = new Color(HoverGlow.r, HoverGlow.g, HoverGlow.b, 0.8f + 0.2f * Mathf.Sin(Time.unscaledTime * 5f));
        else if (equipped) glow.color = new Color(EquippedGlow.r, EquippedGlow.g, EquippedGlow.b, 0.9f);
    }

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
        Sprite(parent, "Banner" + text, x, y, w, h, BannerSprite, Color.white);
        var t = Ui.Text(parent, "BannerText" + text, x, y, w, h, font, 25f, Theme.GoldText, TextAlignmentOptions.CenterGeoAligned);
        t.text = text;
        return y + h;
    }
}
