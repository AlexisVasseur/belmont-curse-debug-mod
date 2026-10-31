using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BelmontPractice;

/// <summary>
/// Affichage uGUI du panneau des savestates, dans le style de l'interface du jeu.
/// Hiérarchie volontairement plate (tout est enfant du fond du panneau) : la position de chaque
/// élément est connue, et la souris est testée sans passer par l'EventSystem du jeu.
/// </summary>
public class SaveStatesView
{
    const float W = 780f, Top = 40f, BannerH = 64f, RowH = 38f, Pad = 16f;
    const int Rows = SlotStore.SlotsPerPage;

    readonly SaveStatesPanel p;
    GameObject root, panel, toast;
    Canvas canvas;
    Transform fill;
    bool builtWithGameFonts;
    bool textInputHooked;

    readonly List<(UiButton btn, float x, float y, float w, float h)> buttons = new();

    TextMeshProUGUI quickName, pageLabel, pageConfirm, toastText;
    UiButton removePage, addPage, pageYes, pageNo;
    float toastUntil;
    const float ToastW = 640f, ToastH = 42f;
    Image toastAccent, toastFrame, toastBack;

    class RowUi
    {
        public Image highlight, line, diaBorder, diaFill;
        public Image selection, caret; // saisie du nom : sélection (sous le texte) et curseur (sur le texte)
        public TextMeshProUGUI digit, name, confirm;
        public UiButton read, write, rename, del, yes, no;
        public float y;
        public float lastClick = -10f;
    }
    RowUi[] rows;

    class Hint { public Bind bind; public SelectOperation op; public TextMeshProUGUI label; public GameObject go; }
    readonly List<Hint> hints = new();

    // Mise en page (repère du fond du panneau) : en-tête (titre, quickslot, pages), liste, pied de page.
    // M : marge du contenu, pour que textes et boutons ne touchent jamais le cadre ni ses coins.
    const float M = 30f, HeadPad = 6f, QuickH = 46f, PagesH = 40f, FootH = 52f;
    static float HeadBottom => BannerH + HeadPad + QuickH + PagesH + HeadPad;
    static float RowsTop => HeadBottom + 4;
    static float FootTop => RowsTop + Rows * RowH + 4;
    static float H => FootTop + FootH;

    public SaveStatesView(SaveStatesPanel panel) { p = panel; }

    // ---------- Construction ----------

    void EnsureRoot()
    {
        if (root != null) return;
        UiCanvas.Ensure();
        root = UiCanvas.Root;
        canvas = UiCanvas.Canvas;
        BuildToast();
    }

    void BuildToast()
    {
        // En bas à gauche de l'écran (en haut, il recouvrait la barre de vie du HUD)
        var bg = Ui.Frame(root.transform, "Toast", 0f, 0f, ToastW, ToastH, Theme.Gold, Theme.Bg);
        var rt = bg.transform.parent.Cast<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0f);
        rt.pivot = new Vector2(0f, 0f);
        rt.anchoredPosition = new Vector2(44f, 28f);
        toast = rt.gameObject;
        toastAccent = Ui.Image(bg.transform, "Accent", 0f, 0f, 4f, ToastH - 2f, Theme.GoldBright);
        if (Skin.BarMana != null && Skin.BarLife != null)
        {
            // Contour des barres de vie / mana du HUD : liseré bleu pour les messages, rouge pour les erreurs
            Ui.SetBorder(bg, new Color(0f, 0f, 0f, 0f));
            bg.color = new Color(0f, 0f, 0f, 0f);
            if (Skin.BarMask != null)
            {
                toastBack = Skin.Sliced(bg.transform, "Back", 4f, 6f, ToastW + 14f, ToastH - 12f, Skin.BarMask, Theme.Hex(0x120c0a, 0.92f), 1.6f);
                toastBack.transform.SetAsFirstSibling();
            }
            // L'image a une marge transparente avant son liseré : on la décale pour que le texte soit à l'intérieur
            toastFrame = Skin.Sliced(bg.transform, "Frame", -31f, -6f, ToastW + 96f, ToastH + 12f, Skin.BarMana, Color.white, 1.4f);
            Ui.SetActive(toastAccent, false);
        }
        toastText = Ui.Text(bg.transform, "Text", 18f, 0f, ToastW - 30f, ToastH - 2f, GameFonts.Body, 20f, Theme.Text);
        toast.SetActive(false);
    }

    void BuildPanel()
    {
        if (panel != null) UnityEngine.Object.Destroy(panel);
        buttons.Clear();
        hints.Clear();
        keyHits.Clear();
        builtWithGameFonts = GameFonts.Resolved;

        var t = GameFonts.Title;
        var b = GameFonts.Body;
        var n = GameFonts.Numbers;

        // Conteneur centré en haut de l'écran
        var container = Ui.Rect(root.transform, "SavestatesPanel", 0, 0, W, H);
        container.anchorMin = new Vector2(0.5f, 1f);
        container.anchorMax = new Vector2(0.5f, 1f);
        container.pivot = new Vector2(0.5f, 1f);
        container.anchoredPosition = new Vector2(0f, -Top);
        panel = container.gameObject;

        var frameFill = Ui.Frame(container, "Frame", 0, 0, W, H, Theme.Gold, Theme.Bg);
        fill = frameFill.transform;
        float iw = W - 2; // largeur intérieure
        float right = iw - M;

        // --- Bandeau titre : bordeaux uni sur toute la largeur, titre, puis un filet doré à losange central
        //     (dessiné pour le mod, s'estompe aux deux bouts) ---
        Ui.Image(fill, "Banner", 0, 0, iw, BannerH, Theme.Crimson);
        Ui.Text(fill, "Title", 0, 2, iw, BannerH - 14, t, 30f, Theme.GoldText, TextAlignmentOptions.CenterGeoAligned).text = "Savestates";
        var rule = GameSprites.Icon("ui_rule");
        if (rule != null) Skin.Sprite(fill, "BannerRule", M + 20, BannerH - 16, iw - 2 * (M + 20), 12, rule, FrameGold);
        else Ui.Image(fill, "BannerRule", M + 20, BannerH - 11, iw - 2 * (M + 20), 2, FrameGold);

        // --- En-tête : bande un peu plus claire, bordée d'un filet doré ---
        Ui.Image(fill, "HeadBand", 0, BannerH, iw, HeadBottom - BannerH, Theme.Hex(0x261a16));
        Ui.Image(fill, "HeadRule", 0, HeadBottom, iw, 1, FrameGold);

        // Quickslot : libellé, nom, puis [F6] Load  [F5] Save à droite (touches + action, sans bouton)
        float qy = BannerH + HeadPad, qky = qy + (QuickH - KeyH) / 2f;
        var ql = Ui.Text(fill, "QuickLabel", M, qy, 110, QuickH, t, 15f, Theme.GoldLabel);
        ql.text = "QUICKSLOT";
        ql.characterSpacing = 6f;
        float saveW = KeyAction(right, qky, Keybinds.QuickSave.ShortLabel, "Save", () => p.QuickSave(), alignRight: true);
        float loadW = KeyAction(right - saveW - 24, qky, Keybinds.QuickLoad.ShortLabel, "Load", () => p.QuickLoad(), alignRight: true);
        float qx = M + 116;
        quickName = Ui.Text(fill, "QuickName", qx, qy, right - saveW - 24 - loadW - 20 - qx, QuickH, b, 21f, Theme.Text);

        // Pages : [4] Page 1 / 7 [6] à gauche ; + Page, Del page à droite (ou la confirmation de suppression)
        float py = qy + QuickH, pky = py + (PagesH - KeyH) / 2f, pby = py + (PagesH - 26) / 2f;
        float px = M;
        px += KeyAction(px, pky, Keybinds.PrevPage.ShortLabel, null, () => p.PrevPage()) + 8;
        pageLabel = Ui.Text(fill, "PageLabel", px, py, 116, PagesH, t, 19f, Theme.GoldText, TextAlignmentOptions.Center);
        px += 116 + 8;
        KeyAction(px, pky, Keybinds.NextPage.ShortLabel, null, () => p.NextPage());

        removePage = Button(right - 88, pby, 88, 26, "Del page", t, 14f, () => p.RemovePage());
        addPage = Button(right - 88 - 8 - 80, pby, 80, 26, "+ Page", t, 14f, () => { p.AddPage(); p.Notify?.Invoke($"Page {p.CurrentPage + 1} added", false); });
        pageNo = Button(right - 56, pby, 56, 26, "No", t, 15f, () => p.ConfirmNo());
        pageYes = Button(right - 56 - 6 - 56, pby, 56, 26, "Yes", t, 15f, () => p.ConfirmYes());
        pageConfirm = Ui.Text(fill, "PageConfirm", right - 118 - 10 - 150, py, 150, PagesH, t, 15f, Theme.Red, TextAlignmentOptions.MidlineRight);
        pageConfirm.text = "Delete page?";

        // --- Emplacements : surbrillance sur toute la largeur, contenu à M des bords ---
        rows = new RowUi[Rows];
        float quickX = right - 78 - 8 - 100, trashX = quickX - 10 - 30, pencilX = trashX - 4 - 30;
        for (int i = 0; i < Rows; i++)
        {
            int index = i;
            float y = RowsTop + i * RowH;
            var r = new RowUi { y = y };
            r.highlight = Ui.Image(fill, $"Row{i}Highlight", 0, y, iw, RowH, Theme.Crimson);
            // Filet entre deux lignes seulement : le pied de page a le sien
            if (i < Rows - 1) r.line = Ui.Image(fill, $"Row{i}Line", M - 10, y + RowH - 1, iw - 2 * (M - 10), 1, Theme.Line);

            var d = Ui.Diamond(fill, $"Row{i}Diamond", M + 12, y + RowH / 2f, 24, n, 22f);
            r.diaBorder = d.border; r.diaFill = d.fill; r.digit = d.label;
            r.digit.text = index.ToString();

            r.selection = Ui.Image(fill, $"Row{i}Selection", NameX, y + 7, 2, RowH - 14, Theme.Hex(0x3d6fb0, 0.85f)); // créée avant : sous le texte
            r.selection.gameObject.SetActive(false);
            r.name = Ui.Text(fill, $"Row{i}Name", NameX, y, pencilX - 10 - NameX, RowH, b, 21f, Theme.Text);
            r.caret = Ui.Image(fill, $"Row{i}Caret", NameX, y + 8, 2, RowH - 16, Theme.GoldText);
            r.caret.gameObject.SetActive(false);
            r.confirm = Ui.Text(fill, $"Row{i}Confirm", right - 118 - 10 - 150, y, 150, RowH, t, 15f, Theme.Red, TextAlignmentOptions.MidlineRight);

            r.write = Button(right - 78, y + 5, 78, 28, "Write", t, 15f, () => p.ClickWrite(index));
            // Met cette savestate dans le quickslot (ancien « Read »)
            r.read = Button(quickX, y + 5, 100, 28, "Quickslot", t, 15f, () => p.ClickRead(index));
            r.del = IconButton(trashX, y + 5, "Delete", "ui_trash", () => p.ClickDelete(index));
            r.rename = IconButton(pencilX, y + 5, "Rename", "ui_pencil", () => p.StartRename(index));
            // Confirmation
            r.no = Button(right - 56, y + 5, 56, 28, "No", t, 15f, () => p.ConfirmNo());
            r.yes = Button(right - 56 - 6 - 56, y + 5, 56, 28, "Yes", t, 15f, () => p.ConfirmYes());
            rows[i] = r;
        }

        // --- Pied de page : bande plus sombre, un seul filet doré, touches + action ---
        Ui.Image(fill, "FootBand", 0, FootTop, iw, H - 2 - FootTop, Theme.Hex(0x120c0b));
        Ui.Image(fill, "FootRule", 0, FootTop, iw, 1, FrameGold);
        float fy = FootTop + (FootH - KeyH) / 2f;
        float hx = M;
        hx = AddHint(hx, fy, Keybinds.SaveToFile, "Save to slot", SelectOperation.SaveToFile);
        hx = AddHint(hx, fy, Keybinds.LoadFromFile, "Load slot", SelectOperation.LoadFromFile);
        hx = AddHint(hx, fy, Keybinds.FileToQuickslot, "Quickslot", SelectOperation.FileToQuickslot);
        hx = AddHint(hx, fy, Keybinds.QuickslotToFile, "Write", SelectOperation.QuickslotToFile);
        KeyAction(right, fy, Keybinds.TogglePanel.ShortLabel, "Close", () => p.Visible = false, alignRight: true, textFirst: true);

        // --- Cadre des cartes d'arcane du jeu, par-dessus tout ; à défaut, le filet doré du fond reste ---
        if (CardFrame(container)) Ui.SetBorder(frameFill, new Color(0f, 0f, 0f, 0f));

        Plugin.Logger.LogInfo($"[Interface] panneau construit (polices du jeu : {(builtWithGameFonts ? "oui" : "non")})");
    }

    float AddHint(float x, float y, Bind bind, string text, SelectOperation op)
    {
        if (!bind.HasKeys) return x;
        var labels = new List<TextMeshProUGUI>();
        float w = KeyAction(x, y, bind.ShortLabel, text, () => p.EnterSelectState(op), labelSink: labels);
        if (labels.Count > 0) hints.Add(new Hint { bind = bind, op = op, label = labels[0], go = labels[0].gameObject });
        return x + w + 26;
    }

    // ---------- Touches de clavier cliquables (cadre de touche du jeu, comme le « T » de la carte) ----------

    const float KeyH = 30f;

    class KeyHit { public float x, y, w, h; public Action act; public TextMeshProUGUI key; }
    readonly List<KeyHit> keyHits = new();

    /// <summary>
    /// Touche (cadre du jeu) et son action, cliquables ensemble. alignRight : l'ensemble se termine à x ;
    /// textFirst : l'action avant la touche (« Close [F1] »). labelSink reçoit le libellé (couleur d'opération en cours).
    /// Retourne la largeur totale.
    /// </summary>
    float KeyAction(float x, float y, string key, string text, Action onClick, bool alignRight = false, bool textFirst = false, List<TextMeshProUGUI> labelSink = null)
    {
        if (string.IsNullOrEmpty(key)) key = "?";
        float kw = Mathf.Max(KeyH, 14f + 11f * key.Length);
        TextMeshProUGUI label = null;
        float lw = 0f, gap = 0f;
        if (!string.IsNullOrEmpty(text))
        {
            label = Ui.Text(fill, "KeyLabel" + text, 0, y, 220, KeyH, GameFonts.Title, 16f, Theme.GoldLabel);
            label.text = text;
            label.margin = new Vector4(0f, 2f, 0f, 0f); // la ligne de base des polices du jeu tombe bas
            lw = Mathf.Ceil(label.preferredWidth) + 2f;
            gap = 8f;
        }
        float total = kw + gap + lw;
        float x0 = alignRight ? x - total : x;
        float kx = textFirst ? x0 + lw + gap : x0, lx = textFirst ? x0 : x0 + kw + gap;
        var sprite = GameSprites.Icon("ui_key_frame", new Vector4(40, 40, 40, 40));
        if (sprite != null) Skin.Sliced(fill, "Key" + key, kx, y, kw, KeyH, sprite, Color.white, 4f);
        else Ui.Image(fill, "Key" + key, kx, y, kw, KeyH, Theme.BgSolid);
        var kt = Ui.Label(fill, "KeyText" + key, kx, y, kw, KeyH, GameFonts.Title, 17f, Theme.GoldText);
        kt.text = key;
        if (label != null)
        {
            Ui.Place(label.rectTransform, lx, y, lw, KeyH);
            labelSink?.Add(label);
        }
        keyHits.Add(new KeyHit { x = x0, y = y, w = total, h = KeyH, act = onClick, key = kt });
        return total;
    }

    // ---------- Cadre des cartes d'arcane (texture du jeu 1127 × 2048, affichée à 40 %) ----------

    const string CardFrameTex = "ui_card_frame";
    const float CardScale = 0.4f;
    // Doré du cadre et du filet du titre. La texture du cadre est un masque blanc (forme du cadre du jeu) teinté de
    // cette seule couleur : les dégradés de l'original faisaient des cassures entre les morceaux.
    static readonly Color FrameGold = Theme.Hex(0xc9a24f);

    /// <summary>
    /// Pose le cadre des cartes d'arcane autour du panneau : coins arrondis et pointes découpés dans la forme du cadre
    /// du jeu, bords droits étirés à partir de la colonne (ou ligne) qui touche les coins, pour un raccord sans couture.
    /// False si la texture manque.
    /// </summary>
    bool CardFrame(RectTransform container)
    {
        const float s = CardScale;
        Sprite P(float x, float y, float w, float h) => GameSprites.Part(CardFrameTex, x, y, w, h);
        void I(string name, Sprite sp, float x, float y, float w, float h) { if (sp != null) Skin.Sprite(container, "Card" + name, x, y, w, h, sp, FrameGold); }
        var tl = P(0, 0, 130, 130);
        if (tl == null) return false;
        float c = 130 * s, e = 14 * s;
        // Bords droits (1 px de large, pris juste après le coin), puis pointes et coins par-dessus
        I("Top", P(131, 0, 1, 14), c, 0, W - 2 * c, e);
        I("Bottom", P(131, 2034, 1, 14), c, H - e, W - 2 * c, e);
        I("Left", P(0, 131, 14, 1), 0, c, e, H - 2 * c);
        I("Right", P(1113, 131, 14, 1), W - e, c, e, H - 2 * c);
        I("NotchTop", P(525, 0, 77, 34), W / 2f - 77 * s / 2f, 0, 77 * s, 34 * s);
        I("NotchBottom", P(525, 2014, 77, 34), W / 2f - 77 * s / 2f, H - 34 * s, 77 * s, 34 * s);
        I("NotchLeft", P(0, 990, 36, 68), 0, H / 2f - 68 * s / 2f, 36 * s, 68 * s);
        I("NotchRight", P(1091, 990, 36, 68), W - 36 * s, H / 2f - 68 * s / 2f, 36 * s, 68 * s);
        I("TL", tl, 0, 0, c, c);
        I("TR", P(997, 0, 130, 130), W - c, 0, c, c);
        I("BL", P(0, 1918, 130, 130), 0, H - c, c, c);
        I("BR", P(997, 1918, 130, 130), W - c, H - c, c, c);
        return true;
    }

    UiButton Button(float x, float y, float w, float h, string text, TMP_FontAsset font, float size, Action onClick)
    {
        var btn = UiButton.CreateSkinned(fill, "Btn" + text, x, y, w, h, text, font, size, onClick);
        buttons.Add((btn, x, y, w, h));
        return btn;
    }

    /// <summary>Petit bouton à icône (texte de secours si l'icône manque).</summary>
    UiButton IconButton(float x, float y, string text, string icon, Action onClick)
    {
        var btn = Button(x, y, 30, 28, text, GameFonts.Title, 11f, onClick);
        if (btn.Skinned) { btn.OrnamentL = btn.OrnamentR = null; } // trop petit pour porter des volutes
        btn.SetIcon(GameSprites.Icon(icon), 16f);
        return btn;
    }

    // ---------- Mise à jour (chaque frame) ----------

    public void Update()
    {
        EnsureRoot();
        UpdateToast();

        bool visible = p.ShouldBeVisible;
        if (!visible) { if (panel != null && panel.activeSelf) panel.SetActive(false); return; }

        if (!GameFonts.Resolved) GameFonts.Resolve();
        if (panel == null || (!builtWithGameFonts && GameFonts.Resolved)) BuildPanel();
        if (!panel.activeSelf) panel.SetActive(true);
        HookTextInput();

        Refresh();
        HandleMouse();
    }

    void Refresh()
    {
        bool pageConfirmActive = p.Confirm == ConfirmKind.RemovePage;

        var q = p.Quickslot;
        Ui.SetText(quickName, q?.Name ?? "Empty");
        quickName.color = q == null ? Theme.TextDim : Theme.Text;

        Ui.SetText(pageLabel, $"Page {p.CurrentPage + 1} / {SlotStore.PageCount}");
        Ui.SetActive(removePage.Rt, !pageConfirmActive);
        Ui.SetActive(addPage.Rt, !pageConfirmActive);
        Ui.SetActive(pageConfirm, pageConfirmActive);
        Ui.SetActive(pageYes.Rt, pageConfirmActive);
        Ui.SetActive(pageNo.Rt, pageConfirmActive);

        GetMouse(out float mx, out float my, out bool hasMouse);

        for (int i = 0; i < Rows; i++)
        {
            var r = rows[i];
            var s = SlotStore.Get(p.CurrentPage, i);
            bool renaming = p.RenameIndex == i;
            bool confirming = p.ConfirmIndex == i && p.Confirm is ConfirmKind.Overwrite or ConfirmKind.DeleteState;
            bool hover = hasMouse && my >= r.y && my < r.y + RowH && mx >= 12 && mx < W - 14;

            string name = !renaming ? s?.Name ?? "Empty" : p.RenameText;
            r.name.overflowMode = renaming ? TextOverflowModes.Overflow : TextOverflowModes.Ellipsis; // saisie : texte entier
            Ui.SetText(r.name, name);
            r.name.color = renaming || hover ? Theme.TextSelected : s == null ? Theme.TextDim : Theme.Text;
            UpdateRenameMarks(r, renaming);

            bool lit = hover || renaming || confirming;
            Ui.SetActive(r.highlight, lit);

            r.diaBorder.color = p.InSelectState ? Theme.Red : s == null ? Theme.Gold : (lit ? Theme.GoldText : Theme.GoldBright);
            r.diaFill.color = lit ? Theme.CrimsonDark : Theme.BgSolid;
            r.digit.color = s == null && !p.InSelectState ? Theme.TextDim : Theme.KeyText;

            Ui.SetActive(r.confirm, confirming);
            if (confirming) Ui.SetText(r.confirm, p.Confirm == ConfirmKind.Overwrite ? "Overwrite?" : "Delete?");
            Ui.SetActive(r.yes.Rt, confirming);
            Ui.SetActive(r.no.Rt, confirming);

            bool normal = !confirming && !renaming;
            Ui.SetActive(r.read.Rt, normal);
            Ui.SetActive(r.write.Rt, normal);
            r.read.Enabled = s != null || p.IsReadOperation;
            // Crayon et poubelle : seulement sur un emplacement occupé, hors choix d'emplacement
            bool tools = normal && s != null && !p.InSelectState;
            Ui.SetActive(r.rename.Rt, tools);
            Ui.SetActive(r.del.Rt, tools);

            // Opération en cours : les boutons concernés sont mis en avant
            r.read.LabelColor = p.IsReadOperation ? Theme.Red : r.read.Skinned ? Skin.ButtonText : Theme.KeyText;
            r.write.LabelColor = p.IsWriteOperation ? Theme.Red : r.write.Skinned ? Skin.ButtonText : Theme.KeyText;
        }

        foreach (var h in hints) h.label.color = p.SelectOp == h.op ? Theme.Red : Theme.GoldLabel;
    }

    // ---------- Saisie du nom : sélection et curseur dessinés sous / sur le texte ----------

    const float NameX = M + 38; // abscisse du nom dans le panneau (après le losange du numéro)

    /// <summary>Abscisse (depuis le bord gauche du nom) de la limite avant le caractère k (k = longueur : après le dernier).</summary>
    static float CharX(TextMeshProUGUI t, int k)
    {
        var info = t.textInfo;
        int n = info?.characterCount ?? 0;
        if (n == 0) return 0f;
        float left = t.rectTransform.rect.xMin;
        if (k < n) return info.characterInfo[k].origin - left;
        return info.characterInfo[n - 1].xAdvance - left;
    }

    /// <summary>Limite entre caractères la plus proche de x (depuis le bord gauche du nom).</summary>
    static int IndexAt(TextMeshProUGUI t, float x, int length)
    {
        int best = 0;
        float bestDist = float.MaxValue;
        for (int k = 0; k <= length; k++)
        {
            float d = Mathf.Abs(CharX(t, k) - x);
            if (d < bestDist) { bestDist = d; best = k; }
        }
        return best;
    }

    void UpdateRenameMarks(RowUi r, bool renaming)
    {
        if (!renaming) { Ui.SetActive(r.selection, false); Ui.SetActive(r.caret, false); return; }
        r.name.ForceMeshUpdate(); // positions des caractères du texte en cours
        if (p.HasSelection)
        {
            float x0 = CharX(r.name, p.SelectionStart), x1 = CharX(r.name, p.SelectionEnd);
            Ui.Place(r.selection.rectTransform, NameX + x0, r.y + 7, Mathf.Max(2f, x1 - x0), RowH - 14);
            Ui.SetActive(r.selection, true);
        }
        else Ui.SetActive(r.selection, false);
        // Curseur clignotant (toujours visible pendant un glisser à la souris)
        bool on = dragRow == r || (Time.unscaledTime % 1f) < 0.5f;
        Ui.Place(r.caret.rectTransform, NameX + CharX(r.name, p.RenameCursor) - 1f, r.y + 8, 2f, RowH - 16);
        Ui.SetActive(r.caret, on);
    }

    RowUi dragRow; // glisser à la souris en cours dans ce nom
    float lastNameClick = -10f;

    /// <summary>
    /// Souris dans le nom en cours de saisie : clic = curseur, glisser = sélection, Maj+clic = étendre,
    /// double-clic = tout sélectionner.
    /// </summary>
    void HandleRenameMouse(float mx, float my, Mouse mouse)
    {
        if (!p.IsTyping) { dragRow = null; return; }
        var r = rows[p.RenameIndex];
        float localX = mx - NameX;
        bool overName = my >= r.y && my < r.y + RowH && mx >= NameX - 8 && mx < W - 14;
        if (mouse.leftButton.wasPressedThisFrame && overName)
        {
            r.name.ForceMeshUpdate();
            var kb = Keyboard.current;
            bool shift = kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
            if (Time.unscaledTime - lastNameClick < 0.35f) { p.SelectAll(); lastNameClick = -10f; dragRow = null; return; }
            lastNameClick = Time.unscaledTime;
            p.SetCaret(IndexAt(r.name, localX, p.RenameText.Length), shift);
            dragRow = r;
        }
        else if (dragRow == r && mouse.leftButton.isPressed)
            p.SetCaret(IndexAt(r.name, localX, p.RenameText.Length), true);
        if (!mouse.leftButton.isPressed) dragRow = null;
    }

    // ---------- Souris ----------

    /// <summary>Position de la souris dans le repère du panneau (pixels de référence, origine en haut à gauche du fond).</summary>
    void GetMouse(out float x, out float y, out bool ok)
    {
        ok = UiCanvas.Mouse(out x, out y);
        if (!ok) return;
        x -= UiCanvas.Width / 2f - W / 2f + 1f; // +1 : épaisseur du cadre
        y -= Top + 1f;
    }

    void HandleMouse()
    {
        GetMouse(out float mx, out float my, out bool ok);
        if (!ok) return;
        var mouse = Mouse.current;
        bool click = mouse.leftButton.wasPressedThisFrame;
        HandleRenameMouse(mx, my, mouse);

        bool handled = false;
        foreach (var (btn, x, y, w, h) in buttons)
        {
            if (!btn.Visible) continue;
            bool hover = mx >= x && mx < x + w && my >= y && my < y + h;
            btn.SetHover(hover);
            if (click && hover && btn.Enabled && !handled)
            {
                handled = true;
                try { btn.OnClick?.Invoke(); }
                catch (Exception ex) { Plugin.Logger.LogError("[Interface] " + ex); }
            }
        }
        // Touches + action (quickslot, pages, pied de page) : survol = touche éclairée, clic = l'action
        foreach (var k in keyHits)
        {
            bool hover = mx >= k.x && mx < k.x + k.w && my >= k.y && my < k.y + k.h;
            k.key.color = hover ? Theme.TextSelected : Theme.GoldText;
            if (click && hover && !handled)
            {
                handled = true;
                try { k.act?.Invoke(); }
                catch (Exception ex) { Plugin.Logger.LogError("[Interface] " + ex); }
            }
        }

        // Double-clic sur le nom d'un emplacement : chargement direct
        if (click && !handled && !p.IsTyping)
        {
            for (int i = 0; i < Rows; i++)
            {
                var r = rows[i];
                if (my < r.y || my >= r.y + RowH || mx < M || mx >= W - M - 270) continue;
                if (Time.unscaledTime - r.lastClick < 0.35f) { r.lastClick = -10f; p.LoadSlot(i); }
                else r.lastClick = Time.unscaledTime;
                break;
            }
        }
    }

    // ---------- Saisie du nom ----------

    void HookTextInput()
    {
        if (textInputHooked) return;
        var kb = Keyboard.current;
        if (kb == null) return;
        try
        {
            kb.add_onTextInput((Il2CppSystem.Action<char>)(Action<char>)p.OnTextInput);
            textInputHooked = true;
        }
        catch (Exception ex)
        {
            textInputHooked = true; // ne pas réessayer à chaque frame
            Plugin.Logger.LogWarning("[Interface] saisie de texte indisponible : " + ex.Message);
        }
    }

    // ---------- Messages ----------

    public void ShowToast(string msg, bool error)
    {
        EnsureRoot();
        if (!GameFonts.Resolved) GameFonts.Resolve();
        if (GameFonts.Body != null) toastText.font = GameFonts.Body;
        Ui.SetText(toastText, msg);
        toastText.color = error ? Theme.Red : Theme.Text;
        if (toastAccent != null) toastAccent.color = error ? Theme.Red : Theme.GoldBright;
        if (toastFrame != null) { toastFrame.sprite = error ? Skin.BarLife : Skin.BarMana; toastFrame.color = Color.white; }
        toastUntil = Time.unscaledTime + 3f;
    }

    void UpdateToast()
    {
        bool show = toastUntil > Time.unscaledTime;
        if (toast != null && toast.activeSelf != show) toast.SetActive(show);
        if (show && !GameFonts.Resolved) return;
        if (show && toastText.font != GameFonts.Body && GameFonts.Body != null) toastText.font = GameFonts.Body;
    }
}
