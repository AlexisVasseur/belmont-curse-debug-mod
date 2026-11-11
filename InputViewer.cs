using System;
using System.Collections.Generic;
using MotherBase.ActionFramework;
using MotherBase.EntityFramework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;
using PlayerInput = MotherBase.ActionFramework.PlayerInput;

namespace BelmontPractice;

/// <summary>
/// Input viewer (option « Input viewer ») : petit encadré en bas à droite, au-dessus du nom de la salle. Deux dispositions,
/// choisies par l'option « Viewer layout » (Auto : celle du dernier périphérique utilisé) :
/// - Keyboard : direction lue par le jeu (PlayerInput.moveInputValue) et une case par action du personnage, étiquetée
///   avec sa touche clavier / souris ;
/// - Gamepad : manette façon Xbox (gâchettes, boutons de tranche, sticks, croix, A B X Y ronds, View / Menu).
/// </summary>
public static class InputViewer
{
    public const string LayoutAuto = "Auto", LayoutKeyboard = "Keyboard", LayoutGamepad = "Gamepad";
    public static readonly string[] Layouts = { LayoutAuto, LayoutKeyboard, LayoutGamepad };

    static bool lastWasGamepad;
    static bool lastKnown;
    static Vector2 lastPos;
    static bool builtWithGameFont;
    static readonly Color Off = Theme.Hex(0x120c0a, 0.9f);
    static readonly Color On = Theme.Hex(0xb8322f);
    static readonly Color DirOn = Theme.Hex(0xe2c27e);

    /// <param name="playing">En jeu, hors chargement long.</param>
    /// <param name="panelOpen">Panneau latéral ouvert : l'encadré se décale à sa gauche.</param>
    public static void Update(bool playing, bool panelOpen)
    {
        try
        {
            TrackDevice();
            if (!playing || !Plugin.ShowInputViewer.Value) { Keys.Hide(); Pad.Hide(); return; }
            if (builtWithGameFont != (GameFonts.Title != null)) { builtWithGameFont = GameFonts.Title != null; Keys.Destroy(); Pad.Destroy(); }

            // Au-dessus de l'encadré du nom de la salle (72 de haut) quand il est affiché
            lastPos = new Vector2(panelOpen ? -(SidePanelView.PanelWidth + 14f) : -18f, Plugin.ShowRoomName.Value ? 14f + 72f + 8f : 14f);
            bool gamepad = UseGamepad();
            if (gamepad) { Keys.Hide(); Pad.Show(lastPos); }
            else { Pad.Hide(); Keys.Show(lastPos); }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning("[Input viewer] désactivé suite à une erreur : " + ex);
            Plugin.ShowInputViewer.Value = false;
            Keys.Hide();
            Pad.Hide();
        }
    }

    static bool UseGamepad() => Plugin.InputViewerLayout.Value switch
    {
        LayoutKeyboard => false,
        LayoutGamepad => true,
        _ => lastKnown ? lastWasGamepad : Gamepad.current != null,
    };

    /// <summary>Dernier périphérique réellement utilisé (Auto) : manette dès qu'un bouton, un stick ou une gâchette bouge.</summary>
    static void TrackDevice()
    {
        var pad = Gamepad.current;
        if (pad != null && PadActive(pad)) { lastWasGamepad = true; lastKnown = true; return; }
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if ((kb != null && kb.anyKey.isPressed) || (mouse != null && (mouse.leftButton.isPressed || mouse.rightButton.isPressed)))
        {
            lastWasGamepad = false;
            lastKnown = true;
        }
    }

    static bool PadActive(Gamepad p) =>
        p.leftStick.ReadValue().sqrMagnitude > 0.25f || p.rightStick.ReadValue().sqrMagnitude > 0.25f
        || p.leftTrigger.ReadValue() > 0.3f || p.rightTrigger.ReadValue() > 0.3f
        || p.buttonSouth.isPressed || p.buttonEast.isPressed || p.buttonWest.isPressed || p.buttonNorth.isPressed
        || p.leftShoulder.isPressed || p.rightShoulder.isPressed || p.startButton.isPressed || p.selectButton.isPressed
        || p.dpad.up.isPressed || p.dpad.down.isPressed || p.dpad.left.isPressed || p.dpad.right.isPressed
        || p.leftStickButton.isPressed || p.rightStickButton.isPressed;

    static void Place(RectTransform rt, Vector2 pos) { if (rt.anchoredPosition != pos) rt.anchoredPosition = pos; }
    static void Tint(Graphic g, Color c) { if (!Theme.Same(g.color, c)) g.color = c; }

    // ---------- Formes rondes (générées : le jeu n'en fournit pas d'utilisable) ----------

    static Sprite disc, ring;
    static Sprite Disc => disc ??= MakeCircle(0f);
    static Sprite Ring => ring ??= MakeCircle(0.82f);

    /// <summary>Disque (inner = 0) ou anneau (inner = rayon intérieur relatif), bords lissés.</summary>
    static Sprite MakeCircle(float inner)
    {
        const int N = 128;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        float r = N / 2f - 1f, ri = r * inner;
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(N / 2f, N / 2f));
                float a = Mathf.Clamp01(r - d + 0.5f);
                if (inner > 0f) a = Mathf.Min(a, Mathf.Clamp01(d - ri + 0.5f));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        UnityEngine.Object.DontDestroyOnLoad(tex);
        return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
    }

    static Image Round(Transform parent, string name, float cx, float cy, float size, Color color, Sprite sprite)
    {
        var img = Ui.Image(parent, name, cx - size / 2f, cy - size / 2f, size, size, color);
        img.sprite = sprite;
        return img;
    }

    // ---------- Disposition clavier : direction + actions du personnage ----------

    static class Keys
    {
        const int Columns = 4, MaxActions = 12;
        const float P = 10f, Cell = 18f, Gap = 2f, BtnW = 36f, BtnH = 22f, BtnGap = 4f;
        const float CrossSize = Cell * 3 + Gap * 2;
        const float W = P + CrossSize + 12f + Columns * BtnW + (Columns - 1) * BtnGap + P;

        static RectTransform box;
        static PlayerInput builtFor;
        static readonly Fade fade = new(0.3f, 0.15f);
        static readonly Image[] dirs = new Image[9]; // 3 × 3, ligne par ligne depuis le haut ; [4] = centre
        static readonly List<(InputAction action, Image fill)> buttons = new();

        public static void Hide() => fade.Out(box != null ? box.gameObject : null);
        public static void Destroy() { if (box != null) UnityEngine.Object.Destroy(box.gameObject); box = null; }

        public static void Show(Vector2 pos)
        {
            var input = MainCharacterSingleton.TryGetEntity(out Entity e) && e != null ? e.GetComponentInChildren<PlayerInput>(true) : null;
            if (input == null) { Hide(); return; }
            if (box == null || builtFor != input) Build(input);
            fade.In(box.gameObject);
            Place(box, pos);

            Vector2 move = input.moveInputValue;
            int dx = move.x > 0.35f ? 1 : move.x < -0.35f ? -1 : 0;
            int dy = move.y > 0.35f ? 1 : move.y < -0.35f ? -1 : 0;
            int lit = (1 - dy) * 3 + (dx + 1);
            for (int i = 0; i < 9; i++) Tint(dirs[i], i == 4 ? Theme.Line : i == lit ? DirOn : Off);

            foreach (var (action, fill) in buttons)
            {
                bool down = false;
                try { down = action.IsPressed(); } catch { }
                Tint(fill, down ? On : Off);
            }
        }

        /// <summary>Actions du personnage (états tenus puis évènements), sans doublon ni les actions de visée / déplacement.</summary>
        static List<InputAction> Actions(PlayerInput input)
        {
            var list = new List<InputAction>();
            var seen = new HashSet<string>();
            string move = input.data?.moveInput?.action?.name, aim = input.data?.aimInput?.action?.name;
            void Add(InputAction a)
            {
                if (a == null || list.Count >= MaxActions) return;
                string n = a.name ?? "";
                if (n == move || n == aim || !seen.Add(n)) return;
                list.Add(a);
            }
            var states = input.data?.inputStates;
            for (int i = 0; states != null && i < states.Length; i++) Add(states[i]?.input?.action);
            var binds = input.data?.inputBindings;
            for (int i = 0; binds != null && i < binds.Length; i++) Add(binds[i]?.input?.action);
            return list;
        }

        static void Build(PlayerInput input)
        {
            Destroy();
            builtFor = input;
            buttons.Clear();
            var actions = Actions(input);
            int rows = Math.Max(1, (actions.Count + Columns - 1) / Columns);
            float gridH = rows * BtnH + (rows - 1) * BtnGap;
            float h = P * 2 + Math.Max(CrossSize, gridH);

            box = CardFrame.SmallBox(UiCanvas.Root.transform, "InputViewerKeys", W, h, new Vector2(1f, 0f), out var content);
            float cy = (h - CrossSize) / 2f;
            for (int r = 0; r < 3; r++)
                for (int c = 0; c < 3; c++)
                    dirs[r * 3 + c] = Ui.Image(content, $"Dir{r}{c}", P + c * (Cell + Gap), cy + r * (Cell + Gap), Cell, Cell, Off);

            float gx = P + CrossSize + 12f, gy = (h - gridH) / 2f;
            for (int i = 0; i < actions.Count; i++)
            {
                float x = gx + (i % Columns) * (BtnW + BtnGap), y = gy + (i / Columns) * (BtnH + BtnGap);
                var fill = Ui.Frame(content, "Btn" + i, x, y, BtnW, BtnH, Theme.Gold, Off);
                var label = Ui.Label(content, "BtnText" + i, x, y, BtnW, BtnH, GameFonts.Title, 12f, Theme.KeyText);
                label.enableAutoSizing = true;
                label.fontSizeMin = 8f;
                label.fontSizeMax = 12f;
                label.text = KeyLabel(actions[i]);
                buttons.Add((actions[i], fill));
            }
            Plugin.Logger.LogInfo($"[Input viewer] clavier : {actions.Count} action(s) : {string.Join(", ", actions.ConvertAll(a => a.name))}");
        }

        /// <summary>Touche clavier / souris de l'action : première liaison simple.</summary>
        static string KeyLabel(InputAction action)
        {
            try
            {
                var bindings = action.bindings;
                for (int i = 0; i < bindings.Count; i++)
                {
                    var b = bindings[i];
                    if (b.isComposite || b.isPartOfComposite) continue;
                    string path = b.effectivePath ?? "";
                    if (path.StartsWith("<Keyboard>", StringComparison.OrdinalIgnoreCase) || path.StartsWith("<Mouse>", StringComparison.OrdinalIgnoreCase))
                        return Short(path.Substring(path.IndexOf('/') + 1));
                }
            }
            catch { }
            return "?";
        }

        static string Short(string c) => c switch
        {
            "leftButton" => "LMB", "rightButton" => "RMB", "middleButton" => "MMB",
            "space" => "Space", "leftShift" => "Shift", "rightShift" => "Shift", "leftCtrl" => "Ctrl", "rightCtrl" => "Ctrl",
            "leftAlt" => "Alt", "enter" => "Enter", "escape" => "Esc", "tab" => "Tab",
            _ => c.Length <= 2 ? c.ToUpperInvariant() : char.ToUpperInvariant(c[0]) + c.Substring(1),
        };
    }

    // ---------- Disposition manette façon Xbox ----------

    static class Pad
    {
        const float W = 250f, H = 142f;
        const float StickRing = 40f, StickDot = 15f, StickTravel = 12f, Face = 21f, FaceGap = 22f, DpadCell = 13f;

        static RectTransform box;
        static readonly Fade fade = new(0.3f, 0.15f);
        static Image lt, rt, ltFill, rtFill, lb, rb, lsRing, lsDot, rsRing, rsDot, view, menu;
        static Image a, b, x, y;
        static TextMeshProUGUI aT, bT, xT, yT;
        static Image dUp, dDown, dLeft, dRight;
        static float lsCx, lsCy, rsCx, rsCy;

        static readonly Color A = Theme.Hex(0x3fae49), B = Theme.Hex(0xd8343a), X = Theme.Hex(0x2f72b8), Y = Theme.Hex(0xe8b923);
        static readonly Color Dark = Theme.Hex(0x1c1411, 0.95f), RingOff = Theme.Hex(0x8a6d3b), RingOn = Theme.Hex(0xe2c27e);

        public static void Hide() => fade.Out(box != null ? box.gameObject : null);
        public static void Destroy() { if (box != null) UnityEngine.Object.Destroy(box.gameObject); box = null; }

        public static void Show(Vector2 pos)
        {
            if (box == null) Build();
            fade.In(box.gameObject);
            Place(box, pos);
            var p = Gamepad.current;

            float lTrig = p?.leftTrigger.ReadValue() ?? 0f, rTrig = p?.rightTrigger.ReadValue() ?? 0f;
            Bar(ltFill, lTrig);
            Bar(rtFill, rTrig);
            Tint(lb, Pressed(p?.leftShoulder) ? On : Off);
            Tint(rb, Pressed(p?.rightShoulder) ? On : Off);

            Stick(lsDot, lsRing, lsCx, lsCy, p?.leftStick.ReadValue() ?? Vector2.zero, Pressed(p?.leftStickButton));
            Stick(rsDot, rsRing, rsCx, rsCy, p?.rightStick.ReadValue() ?? Vector2.zero, Pressed(p?.rightStickButton));

            FaceButton(a, aT, A, Pressed(p?.buttonSouth));
            FaceButton(b, bT, B, Pressed(p?.buttonEast));
            FaceButton(x, xT, X, Pressed(p?.buttonWest));
            FaceButton(y, yT, Y, Pressed(p?.buttonNorth));

            Tint(dUp, Pressed(p?.dpad.up) ? DirOn : Off);
            Tint(dDown, Pressed(p?.dpad.down) ? DirOn : Off);
            Tint(dLeft, Pressed(p?.dpad.left) ? DirOn : Off);
            Tint(dRight, Pressed(p?.dpad.right) ? DirOn : Off);
            Tint(view, Pressed(p?.selectButton) ? On : Off);
            Tint(menu, Pressed(p?.startButton) ? On : Off);
        }

        static bool Pressed(ButtonControl c) => c != null && c.isPressed;

        static void Bar(Image fill, float v)
        {
            v = Mathf.Clamp01(v);
            var r = fill.rectTransform;
            float full = r.parent.GetComponent<RectTransform>().sizeDelta.x - 2f;
            var size = new Vector2(Mathf.Max(0f, full * v), r.sizeDelta.y);
            if (r.sizeDelta != size) r.sizeDelta = size;
            Tint(fill, v > 0.05f ? On : Off);
        }

        static void Stick(Image dot, Image ringImg, float cx, float cy, Vector2 v, bool click)
        {
            if (v.sqrMagnitude > 1f) v.Normalize();
            var pos = new Vector2(cx - StickDot / 2f + v.x * StickTravel, -(cy - StickDot / 2f - v.y * StickTravel));
            if (dot.rectTransform.anchoredPosition != pos) dot.rectTransform.anchoredPosition = pos;
            Tint(dot, click ? On : v.sqrMagnitude > 0.04f ? DirOn : RingOff);
            Tint(ringImg, click ? On : RingOff);
        }

        static void FaceButton(Image img, TextMeshProUGUI label, Color color, bool down)
        {
            Tint(img, down ? color : Dark);
            Tint(label, down ? Theme.Hex(0x140e0c) : color);
        }

        static void Build()
        {
            Destroy();
            box = CardFrame.SmallBox(UiCanvas.Root.transform, "InputViewerPad", W, H, new Vector2(1f, 0f), out var content);
            var t = content.transform;

            // Gâchettes (remplissage selon la pression) et boutons de tranche
            lt = Ui.Frame(t, "LT", 16, 12, 50, 11, Theme.Gold, Off);
            ltFill = Ui.Image(lt.transform.parent, "LTFill", 1, 1, 0, 9, Off);
            rt = Ui.Frame(t, "RT", W - 66, 12, 50, 11, Theme.Gold, Off);
            rtFill = Ui.Image(rt.transform.parent, "RTFill", 1, 1, 0, 9, Off);
            Small(t, "LTText", 16, 12, 50, 11, "LT");
            Small(t, "RTText", W - 66, 12, 50, 11, "RT");
            lb = Ui.Frame(t, "LB", 16, 27, 50, 13, Theme.Gold, Off);
            rb = Ui.Frame(t, "RB", W - 66, 27, 50, 13, Theme.Gold, Off);
            Small(t, "LBText", 16, 27, 50, 13, "LB");
            Small(t, "RBText", W - 66, 27, 50, 13, "RB");

            // Stick gauche en haut à gauche, A B X Y en haut à droite, croix et stick droit en bas (disposition Xbox)
            lsCx = 48; lsCy = 72;
            lsRing = Round(t, "LSRing", lsCx, lsCy, StickRing, RingOff, Ring);
            lsDot = Round(t, "LSDot", lsCx, lsCy, StickDot, RingOff, Disc);

            float fx = W - 50, fy = 74;
            y = Round(t, "Y", fx, fy - FaceGap, Face, Dark, Disc);
            x = Round(t, "X", fx - FaceGap, fy, Face, Dark, Disc);
            b = Round(t, "B", fx + FaceGap, fy, Face, Dark, Disc);
            a = Round(t, "A", fx, fy + FaceGap, Face, Dark, Disc);
            yT = Letter(t, "YText", fx, fy - FaceGap, "Y", Y);
            xT = Letter(t, "XText", fx - FaceGap, fy, "X", X);
            bT = Letter(t, "BText", fx + FaceGap, fy, "B", B);
            aT = Letter(t, "AText", fx, fy + FaceGap, "A", A);

            view = Round(t, "View", W / 2f - 14, 62, 11, Off, Disc);
            menu = Round(t, "Menu", W / 2f + 14, 62, 11, Off, Disc);

            float dx = 92, dy = 112, c = DpadCell;
            dUp = Ui.Image(t, "DUp", dx - c / 2, dy - c * 1.5f, c, c, Off);
            dDown = Ui.Image(t, "DDown", dx - c / 2, dy + c * 0.5f, c, c, Off);
            dLeft = Ui.Image(t, "DLeft", dx - c * 1.5f, dy - c / 2, c, c, Off);
            dRight = Ui.Image(t, "DRight", dx + c * 0.5f, dy - c / 2, c, c, Off);
            Ui.Image(t, "DCenter", dx - c / 2, dy - c / 2, c, c, Theme.Line);

            rsCx = W - 96; rsCy = 112;
            rsRing = Round(t, "RSRing", rsCx, rsCy, StickRing, RingOff, Ring);
            rsDot = Round(t, "RSDot", rsCx, rsCy, StickDot, RingOff, Disc);
        }

        static void Small(Transform t, string name, float x, float y, float w, float h, string text)
        {
            var l = Ui.Label(t, name, x, y, w, h, GameFonts.Title, 9f, Theme.KeyText);
            l.text = text;
        }

        static TextMeshProUGUI Letter(Transform t, string name, float cx, float cy, string text, Color color)
        {
            var l = Ui.Label(t, name, cx - Face / 2f, cy - Face / 2f, Face, Face, GameFonts.Title, 12f, color);
            l.text = text;
            return l;
        }
    }
}
