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
    UiButton editBtn, addPage, removePage, movePageL, movePageR, pageYes, pageNo;
    float toastUntil;
    const float ToastW = 640f, ToastH = 42f;
    Image toastAccent, toastFrame, toastBack;

    class RowUi
    {
        public Image highlight, tip, line, diaBorder, diaFill;
        public TextMeshProUGUI digit, name, confirm;
        public UiButton read, write, up, down, rename, del, yes, no;
        public float y;
        public float lastClick = -10f;
    }
    RowUi[] rows;

    class Hint { public Bind bind; public SelectOperation op; public TextMeshProUGUI label; public GameObject go; }
    readonly List<Hint> hints = new();

    float H => BannerH + 12 + 34 + 10 + 30 + 10 + Rows * RowH + 12 + 34 + 8;
    float RowsTop => BannerH + 12 + 34 + 10 + 30 + 10;

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

        // Un seul cadre doré autour du fond (le filet extérieur doublait le bord)

        fill = Ui.Frame(container, "Frame", 0, 0, W, H, Theme.Gold, Theme.Bg).transform;
        float iw = W - 2; // largeur intérieure

        // --- Bandeau titre : ruban bordeaux du jeu (à défaut, bande bordeaux à filets dorés) ---
        if (Skin.BannerSprite != null)
        {
            Ui.Image(fill, "BannerShade", 0, 0, iw, BannerH, Theme.Hex(0x0b0706, 0.6f));
            // Le ruban occupe les lignes 8 à 61 de son image (70 px) : on l'agrandit pour que sa partie
            // visible couvre exactement le bandeau, collée au cadre en haut
            float bh = BannerH * 70f / 53f;
            Skin.Sprite(fill, "Banner", 0, -bh * 8f / 70f, iw, bh, Skin.BannerSprite, Color.white);
            Ui.Text(fill, "Title", 0, 0, iw, BannerH, t, 30f, Theme.GoldText, TextAlignmentOptions.CenterGeoAligned).text = "Savestates";
        }
        else
        {
            Ui.Image(fill, "Banner", 0, 0, iw, BannerH, Theme.Crimson);
            Ui.Image(fill, "BannerTop", 0, 0, iw, 2, Theme.GoldBright);
            Ui.Image(fill, "BannerBottom", 0, BannerH - 2, iw, 2, Theme.GoldBright);
            Ui.Text(fill, "Title", 0, 0, iw, BannerH, t, 30f, Theme.GoldText, TextAlignmentOptions.Center).text = "Savestates";
            SmallDiamond(18, BannerH / 2f);
            SmallDiamond(iw - 18, BannerH / 2f);
        }

        // --- Quickslot ---
        float qy = BannerH + 12;
        Ui.Text(fill, "QuickLabel", Pad, qy, 120, 34, t, 19f, Theme.GoldLabel).text = "Quickslot";
        quickName = Ui.Text(fill, "QuickName", Pad + 124, qy, iw - Pad * 2 - 124 - 2 * 150 - 12, 34, b, 21f, Theme.Text);
        KeyButton(iw - Pad - 150, qy + 2, 150, 30, "Save", Keybinds.QuickSave, () => p.QuickSave());
        KeyButton(iw - Pad - 150 - 8 - 150, qy + 2, 150, 30, "Load", Keybinds.QuickLoad, () => p.QuickLoad());

        // --- Pages ---
        float py = qy + 34 + 10;
        editBtn = Button(Pad, py + 2, 70, 26, "Edit", t, 15f, () => p.ToggleEditMode());
        addPage = Button(Pad + 78, py + 2, 30, 26, "+", t, 17f, () => p.AddPage());
        removePage = Button(Pad + 112, py + 2, 30, 26, "-", t, 17f, () => p.RemovePage());
        movePageL = Button(Pad + 146, py + 2, 36, 26, "<<", t, 14f, () => p.MovePage(-1));
        movePageR = Button(Pad + 186, py + 2, 36, 26, ">>", t, 14f, () => p.MovePage(+1));

        float cx = iw / 2f;
        KeyBox(cx - 150, py + 4, Keybinds.PrevPage.ShortLabel, () => p.PrevPage());
        Ui.Image(fill, "PageLineL", cx - 118, py + 15, 34, 1, Theme.LineGold);
        pageLabel = Ui.Text(fill, "PageLabel", cx - 80, py, 160, 30, t, 19f, Theme.GoldText, TextAlignmentOptions.Center);
        Ui.Image(fill, "PageLineR", cx + 84, py + 15, 34, 1, Theme.LineGold);
        KeyBox(cx + 126, py + 4, Keybinds.NextPage.ShortLabel, () => p.NextPage());
        // Nouvelle page après la page courante (toujours disponible, pas seulement en mode Edit)
        Button(cx + 160, py + 2, 74, 26, "+ Page", t, 14f, () => { p.AddPage(); p.Notify?.Invoke($"Page {p.CurrentPage + 1} added", false); });

        pageConfirm = Ui.Text(fill, "PageConfirm", iw - Pad - 260, py, 136, 30, t, 15f, Theme.Red, TextAlignmentOptions.MidlineRight);
        pageConfirm.text = "Delete page?";
        pageYes = Button(iw - Pad - 116, py + 2, 54, 26, "Yes", t, 15f, () => p.ConfirmYes());
        pageNo = Button(iw - Pad - 56, py + 2, 54, 26, "No", t, 15f, () => p.ConfirmNo());

        // --- Emplacements ---
        float rx = 12, rw = iw - 24;
        Ui.Image(fill, "RowsTopLine", rx, RowsTop - 1, rw, 1, Theme.Line);
        rows = new RowUi[Rows];
        for (int i = 0; i < Rows; i++)
        {
            int index = i;
            float y = RowsTop + i * RowH;
            var r = new RowUi { y = y };
            r.highlight = Ui.Image(fill, $"Row{i}Highlight", rx, y, rw - 16, RowH, Theme.Crimson);
            float tipSize = RowH * 0.707f;
            r.tip = Ui.Image(fill, $"Row{i}Tip", rx + rw - 16 - tipSize / 2f, y + RowH / 2f - tipSize / 2f, tipSize, tipSize, Theme.Crimson);
            r.tip.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            r.tip.rectTransform.anchoredPosition = new Vector2(rx + rw - 16, -(y + RowH / 2f));
            r.tip.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
            r.line = Ui.Image(fill, $"Row{i}Line", rx, y + RowH - 1, rw, 1, Theme.Line);

            var d = Ui.Diamond(fill, $"Row{i}Diamond", rx + 26, y + RowH / 2f, 24, n, 22f);
            r.diaBorder = d.border; r.diaFill = d.fill; r.digit = d.label;
            r.digit.text = index.ToString();

            float right = rx + rw - 22;
            r.name = Ui.Text(fill, $"Row{i}Name", rx + 50, y, rw - 50 - 290, RowH, b, 21f, Theme.Text);
            r.confirm = Ui.Text(fill, $"Row{i}Confirm", right - 260, y, 140, RowH, t, 15f, Theme.Red, TextAlignmentOptions.MidlineRight);

            // Mode normal
            r.write = Button(right - 78, y + 5, 78, 28, "Write", t, 15f, () => p.ClickWrite(index));
            r.read = Button(right - 78 - 6 - 78, y + 5, 78, 28, "Read", t, 15f, () => p.ClickRead(index));
            // Mode édition
            r.del = Button(right - 70, y + 5, 70, 28, "Delete", t, 14f, () => p.ClickDelete(index));
            r.rename = Button(right - 70 - 6 - 78, y + 5, 78, 28, "Rename", t, 14f, () => p.StartRename(index));
            r.down = Button(right - 70 - 6 - 78 - 6 - 56, y + 5, 56, 28, "Down", t, 14f, () => p.MoveState(index, +1));
            r.up = Button(right - 70 - 6 - 78 - 6 - 56 - 6 - 44, y + 5, 44, 28, "Up", t, 14f, () => p.MoveState(index, -1));
            // Confirmation
            r.no = Button(right - 56, y + 5, 56, 28, "No", t, 15f, () => p.ConfirmNo());
            r.yes = Button(right - 56 - 6 - 56, y + 5, 56, 28, "Yes", t, 15f, () => p.ConfirmYes());
            rows[i] = r;
        }

        // --- Barre des raccourcis ---
        float fy = RowsTop + Rows * RowH + 12;
        Ui.Image(fill, "FooterLine", Pad, fy - 4, iw - Pad * 2, 1, Theme.LineGold);
        float hx = Pad;
        hx = AddHint(hx, fy, Keybinds.SaveToFile, "Save to", SelectOperation.SaveToFile);
        hx = AddHint(hx, fy, Keybinds.LoadFromFile, "Load", SelectOperation.LoadFromFile);
        hx = AddHint(hx, fy, Keybinds.FileToQuickslot, "Read", SelectOperation.FileToQuickslot);
        hx = AddHint(hx, fy, Keybinds.QuickslotToFile, "Write", SelectOperation.QuickslotToFile);
        var close = Ui.Text(fill, "CloseLabel", iw - Pad - 150, fy, 110, 30, t, 15f, Theme.GoldLabel, TextAlignmentOptions.MidlineRight);
        close.text = "Close";
        KeyBox(iw - Pad - 34, fy + 4, Keybinds.TogglePanel.ShortLabel, () => p.Visible = false);

        Plugin.Logger.LogInfo($"[Interface] panneau construit (polices du jeu : {(builtWithGameFonts ? "oui" : "non")})");
    }

    float AddHint(float x, float y, Bind bind, string text, SelectOperation op)
    {
        if (!bind.HasKeys) return x;
        float kw = KeyBox(x, y + 4, bind.ShortLabel, () => p.EnterSelectState(op));
        var label = Ui.Text(fill, "Hint" + text, x + kw + 6, y, 120, 30, GameFonts.Title, 15f, Theme.GoldLabel);
        label.text = text;
        float lw = Mathf.Max(40f, label.preferredWidth);
        hints.Add(new Hint { bind = bind, op = op, label = label, go = label.gameObject });
        return x + kw + 6 + lw + 22;
    }

    void SmallDiamond(float cx, float cy)
    {
        var img = Ui.Image(fill, "Ornament", 0, 0, 9, 9, Theme.GoldBright);
        img.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        img.rectTransform.anchoredPosition = new Vector2(cx, -cy);
        img.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
    }

    UiButton Button(float x, float y, float w, float h, string text, TMP_FontAsset font, float size, Action onClick)
    {
        var btn = UiButton.CreateSkinned(fill, "Btn" + text, x, y, w, h, text, font, size, onClick);
        buttons.Add((btn, x, y, w, h));
        return btn;
    }

    /// <summary>Bouton avec sa pastille de touche à droite (ex. « Load [F6] »).</summary>
    void KeyButton(float x, float y, float w, float h, string text, Bind bind, Action onClick)
    {
        var btn = Button(x, y, w, h, "", GameFonts.Title, 16f, onClick);
        var label = Ui.Text(btn.Fill.transform, "Text", 14, 0, w - 64, h, GameFonts.Title, 16f, btn.LabelColor, TextAlignmentOptions.MidlineLeft);
        label.text = text;
        label.margin = new Vector4(0f, 2f, 0f, 0f); // la ligne de base des polices du jeu tombe bas
        string key = bind.ShortLabel;
        if (key.Length > 0)
        {
            // Touche en simple texte doré à droite (sans pastille)
            var k = Ui.Text(btn.Fill.transform, "Key", w - 70, 0, 56, h, GameFonts.Title, 15f, Theme.GoldText, TextAlignmentOptions.MidlineRight);
            k.text = key;
            k.margin = new Vector4(0f, 2f, 0f, 0f);
        }
    }

    /// <summary>Pastille de touche cliquable ; retourne sa largeur.</summary>
    float KeyBox(float x, float y, string key, Action onClick)
    {
        if (string.IsNullOrEmpty(key)) key = "?";
        float kw = Mathf.Max(30f, 16f + 9f * key.Length);
        var btn = Button(x, y - 3, kw, 28, key, GameFonts.Title, 15f, onClick);
        if (btn.Skinned) { btn.OrnamentL = btn.OrnamentR = null; } // trop petite pour porter des volutes
        else Ui.SetBorder(btn.Fill, Theme.GoldBright);
        // Chiffre centré sur sa forme, sans risque de disparaître
        btn.Label.alignment = TextAlignmentOptions.MidlineGeoAligned;
        btn.Label.overflowMode = TextOverflowModes.Overflow;
        return kw;
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
        bool edit = p.EditMode;
        bool pageConfirmActive = p.Confirm == ConfirmKind.RemovePage;

        var q = p.Quickslot;
        Ui.SetText(quickName, q?.Name ?? "Empty");
        quickName.color = q == null ? Theme.TextDim : Theme.Text;

        Ui.SetText(pageLabel, $"Page {p.CurrentPage + 1} / {SlotStore.PageCount}");
        editBtn.BaseColor = edit ? Theme.Crimson : editBtn.Skinned ? Skin.ButtonIdle : Theme.BgButton;
        foreach (var b in new[] { addPage, removePage, movePageL, movePageR }) Ui.SetActive(b.Rt, edit && !pageConfirmActive);
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

            string name = renaming
                ? p.RenameText + ((Time.unscaledTime % 1f) < 0.5f ? "_" : " ")
                : s?.Name ?? "Empty";
            Ui.SetText(r.name, name);
            r.name.color = renaming || hover ? Theme.TextSelected : s == null ? Theme.TextDim : Theme.Text;

            bool lit = hover || renaming || confirming;
            Ui.SetActive(r.highlight, lit);
            Ui.SetActive(r.tip, lit);

            r.diaBorder.color = p.InSelectState ? Theme.Red : s == null ? Theme.Gold : (lit ? Theme.GoldText : Theme.GoldBright);
            r.diaFill.color = lit ? Theme.CrimsonDark : Theme.BgSolid;
            r.digit.color = s == null && !p.InSelectState ? Theme.TextDim : Theme.KeyText;

            Ui.SetActive(r.confirm, confirming);
            if (confirming) Ui.SetText(r.confirm, p.Confirm == ConfirmKind.Overwrite ? "Overwrite?" : "Delete?");
            Ui.SetActive(r.yes.Rt, confirming);
            Ui.SetActive(r.no.Rt, confirming);

            bool normal = !confirming && !edit && !renaming;
            bool editing = !confirming && edit && !renaming;
            Ui.SetActive(r.read.Rt, normal);
            Ui.SetActive(r.write.Rt, normal);
            r.read.Enabled = s != null || p.IsReadOperation;
            foreach (var b in new[] { r.up, r.down, r.rename, r.del }) Ui.SetActive(b.Rt, editing);
            r.rename.Enabled = r.del.Enabled = s != null;

            // Opération en cours : les boutons concernés sont mis en avant
            r.read.LabelColor = p.IsReadOperation ? Theme.Red : r.read.Skinned ? Skin.ButtonText : Theme.KeyText;
            r.write.LabelColor = p.IsWriteOperation ? Theme.Red : r.write.Skinned ? Skin.ButtonText : Theme.KeyText;
        }

        foreach (var h in hints) h.label.color = p.SelectOp == h.op ? Theme.Red : Theme.GoldLabel;
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

        // Double-clic sur le nom d'un emplacement : chargement direct
        if (click && !handled && !p.EditMode && !p.IsTyping)
        {
            for (int i = 0; i < Rows; i++)
            {
                var r = rows[i];
                if (my < r.y || my >= r.y + RowH || mx < 12 || mx >= W - 300) continue;
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
