using System;
using System.Collections.Generic;
using MotherBase.ActionFramework;
using MotherBase.EntityFramework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using PlayerInput = MotherBase.ActionFramework.PlayerInput;

namespace BelmontPractice;

/// <summary>
/// Input viewer (option « Input viewer ») : petit encadré en bas à droite, au-dessus du nom de la salle. À gauche la
/// direction lue par le jeu (PlayerInput.moveInputValue, 8 directions), à droite une case par action du personnage
/// (celles de PlayerInputData), étiquetée avec sa touche sur le périphérique utilisé en dernier, allumée quand elle est
/// appuyée. Lit les entrées telles que le jeu les reçoit : clavier et manette.
/// </summary>
public static class InputViewer
{
    const int Columns = 4, MaxActions = 12;
    const float Pad = 10f, Cell = 18f, Gap = 2f, BtnW = 36f, BtnH = 22f, BtnGap = 4f;
    const float CrossSize = Cell * 3 + Gap * 2;
    const float W = Pad + CrossSize + 12f + Columns * BtnW + (Columns - 1) * BtnGap + Pad;

    static GameObject box;
    static RectTransform boxRt;
    static bool builtWithGameFont;
    static PlayerInput builtFor;
    static readonly Fade fade = new(0.3f, 0.15f);
    static readonly Image[] dirs = new Image[9]; // 3 × 3, ligne par ligne depuis le haut ; [4] = centre
    static readonly List<(InputAction action, Image fill, TextMeshProUGUI label)> buttons = new();
    static bool gamepadMode, labelsForGamepad;
    static bool labelsSet;

    static readonly Color Off = Theme.Hex(0x120c0a, 0.9f);
    static readonly Color On = Theme.Hex(0xb8322f);
    static readonly Color DirOn = Theme.Hex(0xe2c27e);

    /// <param name="playing">En jeu, hors chargement long.</param>
    /// <param name="panelOpen">Panneau latéral ouvert : l'encadré se décale à sa gauche.</param>
    public static void Update(bool playing, bool panelOpen)
    {
        try
        {
            if (!playing || !Plugin.ShowInputViewer.Value) { fade.Out(box); return; }
            var input = PlayerInputOf();
            if (input == null) { fade.Out(box); return; }
            if (box == null || builtFor != input || (!builtWithGameFont && GameFonts.Title != null)) Build(input);
            fade.In(box);

            // Au-dessus de l'encadré du nom de la salle (72 de haut) quand il est affiché
            var pos = new Vector2(panelOpen ? -(SidePanelView.PanelWidth + 14f) : -18f, Plugin.ShowRoomName.Value ? 14f + 72f + 8f : 14f);
            if (boxRt.anchoredPosition != pos) boxRt.anchoredPosition = pos;

            // Direction
            Vector2 move = input.moveInputValue;
            int dx = move.x > 0.35f ? 1 : move.x < -0.35f ? -1 : 0;
            int dy = move.y > 0.35f ? 1 : move.y < -0.35f ? -1 : 0;
            int lit = (1 - dy) * 3 + (dx + 1);
            for (int i = 0; i < 9; i++)
            {
                var c = i == lit && i != 4 ? DirOn : i == 4 ? Theme.Line : Off;
                if (!Theme.Same(dirs[i].color, c)) dirs[i].color = c;
            }

            // Actions ; le périphérique de la dernière touche appuyée choisit les étiquettes
            foreach (var (action, fill, _) in buttons)
            {
                bool down = false;
                try { down = action.IsPressed(); } catch { }
                if (down)
                {
                    var dev = action.activeControl?.device;
                    if (dev != null) gamepadMode = dev.TryCast<Gamepad>() != null;
                }
                var c = down ? On : Off;
                if (!Theme.Same(fill.color, c)) fill.color = c;
            }
            if (move.sqrMagnitude > 0.1f && Gamepad.current != null && Gamepad.current.leftStick.ReadValue().sqrMagnitude > 0.1f) gamepadMode = true;
            if (!labelsSet || labelsForGamepad != gamepadMode) SetLabels();
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning("[Input viewer] désactivé suite à une erreur : " + ex.Message);
            Plugin.ShowInputViewer.Value = false;
            fade.Out(box);
        }
    }

    static PlayerInput PlayerInputOf() =>
        MainCharacterSingleton.TryGetEntity(out Entity e) && e != null ? e.GetComponentInChildren<PlayerInput>(true) : null;

    /// <summary>Actions du personnage (états tenus puis évènements), sans doublon ni l'action de déplacement.</summary>
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
        if (box != null) UnityEngine.Object.Destroy(box);
        builtWithGameFont = GameFonts.Title != null;
        builtFor = input;
        buttons.Clear();
        labelsSet = false;
        gamepadMode = Gamepad.current != null;

        var actions = Actions(input);
        int rows = Math.Max(1, (actions.Count + Columns - 1) / Columns);
        float gridH = rows * BtnH + (rows - 1) * BtnGap;
        float h = Pad * 2 + Math.Max(CrossSize, gridH);

        boxRt = CardFrame.SmallBox(UiCanvas.Root.transform, "InputViewer", W, h, new Vector2(1f, 0f), out var content);
        box = boxRt.gameObject;

        float cy = (h - CrossSize) / 2f;
        for (int r = 0; r < 3; r++)
            for (int c = 0; c < 3; c++)
                dirs[r * 3 + c] = Ui.Image(content, $"Dir{r}{c}", Pad + c * (Cell + Gap), cy + r * (Cell + Gap), r == 1 && c == 1 ? Cell : Cell, Cell, Off);

        float gx = Pad + CrossSize + 12f, gy = (h - gridH) / 2f;
        for (int i = 0; i < actions.Count; i++)
        {
            float x = gx + (i % Columns) * (BtnW + BtnGap), y = gy + (i / Columns) * (BtnH + BtnGap);
            var fill = Ui.Frame(content, "Btn" + i, x, y, BtnW, BtnH, Theme.Gold, Off);
            var label = Ui.Label(content, "BtnText" + i, x, y, BtnW, BtnH, GameFonts.Title, 12f, Theme.KeyText);
            label.enableAutoSizing = true;
            label.fontSizeMin = 8f;
            label.fontSizeMax = 12f;
            buttons.Add((actions[i], fill, label));
        }
        Plugin.Logger.LogInfo($"[Input viewer] {actions.Count} action(s) : {string.Join(", ", actions.ConvertAll(a => a.name))}");
    }

    static void SetLabels()
    {
        labelsSet = true;
        labelsForGamepad = gamepadMode;
        foreach (var (action, _, label) in buttons) label.text = KeyLabel(action, gamepadMode);
    }

    /// <summary>Touche de l'action sur le périphérique : première liaison simple de la manette, ou du clavier / souris.</summary>
    static string KeyLabel(InputAction action, bool gamepad)
    {
        try
        {
            var bindings = action.bindings;
            for (int i = 0; i < bindings.Count; i++)
            {
                var b = bindings[i];
                if (b.isComposite || b.isPartOfComposite) continue;
                string path = b.effectivePath ?? "";
                bool pad = path.StartsWith("<Gamepad>", StringComparison.OrdinalIgnoreCase);
                bool kbm = path.StartsWith("<Keyboard>", StringComparison.OrdinalIgnoreCase) || path.StartsWith("<Mouse>", StringComparison.OrdinalIgnoreCase);
                if ((gamepad && pad) || (!gamepad && kbm)) return Short(path);
            }
        }
        catch { }
        return "?";
    }

    static string Short(string path)
    {
        string c = path.Substring(path.IndexOf('/') + 1);
        return c switch
        {
            "buttonSouth" => "A", "buttonEast" => "B", "buttonWest" => "X", "buttonNorth" => "Y",
            "leftShoulder" => "LB", "rightShoulder" => "RB", "leftTrigger" => "LT", "rightTrigger" => "RT",
            "leftStickPress" => "L3", "rightStickPress" => "R3", "start" => "Start", "select" => "Back",
            "dpad/up" => "Up", "dpad/down" => "Down", "dpad/left" => "Left", "dpad/right" => "Right",
            "leftButton" => "LMB", "rightButton" => "RMB", "middleButton" => "MMB",
            "space" => "Space", "leftShift" => "Shift", "rightShift" => "Shift", "leftCtrl" => "Ctrl", "rightCtrl" => "Ctrl",
            "leftAlt" => "Alt", "enter" => "Enter", "escape" => "Esc", "tab" => "Tab",
            _ => c.Length <= 2 ? c.ToUpperInvariant() : char.ToUpperInvariant(c[0]) + c.Substring(1),
        };
    }
}
