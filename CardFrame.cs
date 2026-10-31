using UnityEngine;
using UnityEngine.UI;

namespace BelmontPractice;

/// <summary>
/// Cadre des cartes d'arcane du jeu autour d'une fenêtre du mod (savestates, panneau latéral).
/// ui_card_frame : masque blanc de la forme du cadre du jeu (1127 × 2048), teinté d'un seul doré ; ui_card_inner : la même
/// forme sans les zones situées hors de l'arc intérieur des coins, pour découper les fonds.
/// Les morceaux sont accrochés aux bords et aux coins du conteneur : le cadre suit sa taille, fixe ou étirée.
/// </summary>
public static class CardFrame
{
    public const float Scale = 0.3f; // cadre fin : coins de 39 px, filet de 4 px
    public static readonly Color Gold = Theme.Hex(0xc9a24f);
    const string FrameTex = "ui_card_frame", InnerTex = "ui_card_inner";
    const float Corner = 130f, Edge = 14f; // dans la texture

    static Sprite Part(float x, float y, float w, float h) => GameSprites.Part(FrameTex, x, y, w, h);

    static void Piece(RectTransform parent, string name, Sprite sp, Vector2 aMin, Vector2 aMax, Vector2 offMin, Vector2 offMax)
    {
        if (sp == null) return;
        var go = new GameObject("Card" + name);
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = aMin;
        rt.anchorMax = aMax;
        rt.offsetMin = offMin;
        rt.offsetMax = offMax;
        var img = go.AddComponent<Image>();
        img.sprite = sp;
        img.color = Gold;
        img.raycastTarget = false;
    }

    /// <summary>
    /// Pose le cadre par-dessus le contenu du conteneur (à appeler en dernier). Bords droits étirés à partir de la colonne
    /// ou de la ligne qui touche les coins, pour un raccord sans couture. False si la texture manque.
    /// </summary>
    public static bool Add(RectTransform container)
    {
        if (Part(0, 0, Corner, Corner) == null) return false;
        float s = Scale, c = Corner * s, e = Edge * s;
        float nw = 77 * s, nh = 34 * s, sw = 36 * s, sh = 68 * s;
        Vector2 V(float x, float y) => new(x, y);
        // Bords droits (1 px pris juste après le coin), puis pointes et coins par-dessus
        Piece(container, "Top", Part(131, 0, 1, Edge), V(0, 1), V(1, 1), V(c, -e), V(-c, 0));
        Piece(container, "Bottom", Part(131, 2048 - Edge, 1, Edge), V(0, 0), V(1, 0), V(c, 0), V(-c, e));
        Piece(container, "Left", Part(0, 131, Edge, 1), V(0, 0), V(0, 1), V(0, c), V(e, -c));
        Piece(container, "Right", Part(1127 - Edge, 131, Edge, 1), V(1, 0), V(1, 1), V(-e, c), V(0, -c));
        Piece(container, "NotchTop", Part(525, 0, 77, 34), V(0.5f, 1), V(0.5f, 1), V(-nw / 2, -nh), V(nw / 2, 0));
        Piece(container, "NotchBottom", Part(525, 2014, 77, 34), V(0.5f, 0), V(0.5f, 0), V(-nw / 2, 0), V(nw / 2, nh));
        Piece(container, "NotchLeft", Part(0, 990, 36, 68), V(0, 0.5f), V(0, 0.5f), V(0, -sh / 2), V(sw, sh / 2));
        Piece(container, "NotchRight", Part(1091, 990, 36, 68), V(1, 0.5f), V(1, 0.5f), V(-sw, -sh / 2), V(0, sh / 2));
        Piece(container, "TL", Part(0, 0, Corner, Corner), V(0, 1), V(0, 1), V(0, -c), V(c, 0));
        Piece(container, "TR", Part(1127 - Corner, 0, Corner, Corner), V(1, 1), V(1, 1), V(-c, -c), V(0, 0));
        Piece(container, "BL", Part(0, 2048 - Corner, Corner, Corner), V(0, 0), V(0, 0), V(0, 0), V(c, c));
        Piece(container, "BR", Part(1127 - Corner, 2048 - Corner, Corner, Corner), V(1, 0), V(1, 0), V(-c, 0), V(0, c));
        return true;
    }

    /// <summary>
    /// Masque étiré sur tout le conteneur, à la forme de l'intérieur du cadre : ce qui y est placé (les fonds) ne dépasse
    /// pas entre l'arc des coins et le bord. outset : débord sur le parent (fond décalé de 1 px dans le cadre). À créer avant
    /// le contenu, dans le fond, pour être dessiné dessous. Null si la texture manque (les fonds se posent alors directement).
    /// </summary>
    public static RectTransform Clip(Transform parent, float outset = 0f)
    {
        var inner = GameSprites.Icon(InnerTex, new Vector4(Corner, Corner, Corner, Corner));
        if (inner == null) return null;
        var go = new GameObject("BackgroundClip");
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(-outset, -outset);
        rt.offsetMax = new Vector2(outset, outset);
        var img = go.AddComponent<Image>();
        img.sprite = inner;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 1f / Scale;
        img.raycastTarget = false;
        go.AddComponent<Mask>().showMaskGraphic = false;
        return rt;
    }
}
