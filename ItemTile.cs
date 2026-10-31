using UnityEngine;
using UnityEngine.UI;

namespace BelmontPractice;

/// <summary>
/// Case d'arme ou de relique : cadre esquissé du jeu, contour lumineux qui suit le carré
/// (orange qui pulse au survol, bleu quand l'objet est équipé). Sans les assets : ancien cadre à filets.
/// </summary>
public class ItemTile
{
    public Image icon;
    public float x, y;
    readonly bool skinned;
    Image bg, frame, glow, eqMark;
    /// <summary>Teinte du cadre (couleur du rang de l'arme) ; blanc cassé par défaut.</summary>
    public Color frameTint = Theme.Hex(0xb8a888);
    readonly float size;

    ItemTile(float size, bool skinned) { this.size = size; this.skinned = skinned; }

    public static ItemTile Build(Transform parent, string name, float x, float y, float s)
    {
        var t = new ItemTile(s, Skin.Slot != null) { x = x, y = y };
        if (t.skinned)
        {
            if (Skin.GlowSquare != null) t.glow = Skin.ShapeGlow(parent, name + "Glow", x + s / 2f, y + s / 2f, s, Skin.GlowSquare);
            var outer = Ui.Rect(parent, name, x, y, s, s);
            t.bg = Ui.Image(outer, "Fill", 0, 0, s, s, Theme.Hex(0x0e0a08, 0.95f));
            t.frame = Skin.Sprite(outer, "Slot", -6, -6, s + 12, s + 12, Skin.Slot, Color.white);
            t.icon = Ui.Image(outer, "Icon", 4, 4, s - 8, s - 8, Color.white);
            t.icon.preserveAspect = true;
            return t;
        }
        t.bg = Ui.Frame(parent, name, x, y, s, s, Theme.Gold, Theme.TileOff);
        t.icon = Ui.Image(t.bg.transform, "Icon", 2, 2, s - 6, s - 6, Color.white);
        t.icon.preserveAspect = true;
        t.eqMark = Ui.Image(t.bg.transform, "Equipped", 0, 0, 9, 9, Theme.GoldText);
        t.eqMark.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        t.eqMark.rectTransform.anchoredPosition = new Vector2((s - 2) / 2f, -(s - 4));
        t.eqMark.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
        return t;
    }

    public bool Contains(float mx, float my) => mx >= x && mx < x + size && my >= y && my < y + size;

    public void Apply(bool hover, bool owned, bool equipped)
    {
        icon.color = owned ? Color.white : new Color(0.3f, 0.27f, 0.25f, 0.75f);
        if (!skinned)
        {
            bg.color = hover ? Theme.Crimson : owned ? Theme.TileOn : Theme.TileOff;
            Ui.SetBorder(bg, equipped ? Theme.GoldText : owned ? Theme.GoldBright : Theme.Line);
            Ui.SetActive(eqMark, equipped);
            return;
        }
        // Survol en rouge (comme les lignes des autres écrans), objet équipé en doré animé
        frame.color = hover ? Theme.Hex(0xff8a80) : equipped ? Theme.Hex(0xffd27a) : owned ? frameTint : Theme.Hex(0x4a4038);
        Skin.ApplyItemGlow(glow, hover, equipped);
    }
}
