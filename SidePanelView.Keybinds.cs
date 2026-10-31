using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BelmontPractice;

/// <summary>
/// Onglet Keybinds : chaque raccourci se modifie en cliquant sa ligne puis en pressant une touche
/// (enregistré aussitôt dans le fichier de config BepInEx). Retour arrière / Suppr : aucune touche,
/// Échap : annuler, clic droit : valeur par défaut.
/// </summary>
public partial class SidePanelView
{
    class KeyRowUi
    {
        public Bind bind;           // null pour la ligne « chiffres des emplacements » et les options
        public Option option;       // option activée / désactivée (null pour un raccourci)
        public Image highlight, chipFill;
        public TextMeshProUGUI name, chip;
        public float y;
    }
    readonly List<KeyRowUi> keyRows = new();
    readonly List<(UiButton btn, float x, float y, float w, float h)> keyButtons = new();
    const float KeyRowH = 38f, ChipW = 170f;

    /// <summary>Fonction du mod activée / désactivée (option bool du fichier de config).</summary>
    class Option
    {
        public string Name, Help;
        public Func<BepInEx.Configuration.ConfigEntry<bool>> Entry; // lue à l'usage : les options sont créées après l'interface possible
        public Option(string name, Func<BepInEx.Configuration.ConfigEntry<bool>> entry, string help) { Name = name; Entry = entry; Help = help; }
    }

    static readonly Option[] Options =
    {
        new("Cutscene fast-forward", () => Plugin.CutsceneFastForward,
            "Lets the fast-forward shortcut (F7 / gamepad Y) speed up cutscenes. Off: the shortcut does nothing and a fast-forward in progress stops."),
        new("Short boss intro", () => Plugin.BossShortIntro,
            "Fight: the boss arrives with the short intro the game plays after a defeat, instead of its full entrance cutscene."),
        new("Fast load", () => Plugin.FastLoad,
            "In the same level, savestates load in place without a loading screen (a full load is still used when needed)."),
        new("Reload room", () => Plugin.ReloadRoom,
            "Fast load: the room is reloaded too, so its enemies and objects are reset."),
    };

    static Key[] allKeys;

    float BuildKeybinds(float y)
    {
        keyRows.Clear();
        keyButtons.Clear();
        float iw = W - 1;

        void Row(Bind bind, string name, Option option = null)
        {
            var r = new KeyRowUi { bind = bind, option = option, y = y };
            r.highlight = Ui.Image(fill, "KeyHl" + name, 12, y, iw - 24, KeyRowH - 2, Theme.Hex(0x5e1418, 0.55f));
            Ui.Image(fill, "KeyLine" + name, 12, y + KeyRowH - 1, iw - 24, 1, Theme.Line);
            r.name = Ui.Text(fill, "KeyName" + name, Pad + 4, y, iw - Pad * 2 - ChipW - 12, KeyRowH, GameFonts.Title, 18f, Theme.Text);
            r.name.text = name;
            r.chipFill = Ui.Frame(fill, "KeyChip" + name, iw - Pad - ChipW - 4, y + (KeyRowH - 28) / 2f, ChipW, 26, Theme.Gold, Theme.BgButton);
            r.chip = Ui.Label(r.chipFill.transform, "Text", 0, 0, ChipW - 2, 26, GameFonts.Title, 15f, Theme.KeyText);
            keyRows.Add(r);
            y += KeyRowH;
        }

        y = SectionTitle("Options", y);
        foreach (var o in Options) Row(null, o.Name, o);
        y += 10;

        foreach (var section in new[] { Keybinds.SectionSavestates, Keybinds.SectionUtilities })
        {
            y = SectionTitle(section, y);
            foreach (var b in Keybinds.All.Where(b => b.Section == section)) Row(b, b.Name);
            if (section == Keybinds.SectionSavestates) Row(null, "Slot digits");
            y += 10;
        }

        // Sous les raccourcis : la liste commence directement sous son titre
        var reset = UiButton.CreateSkinned(fill, "BtnResetKeys", Pad, y, iw - Pad * 2, BarH, "Reset all to default", GameFonts.Title, 16f, ResetAllKeys);
        keyButtons.Add((reset, Pad, y, iw - Pad * 2, BarH));
        return y + BarH + 8;
    }

    void ResetAllKeys()
    {
        Keybinds.Capturing = null;
        foreach (var b in Keybinds.All) b.ResetToDefault();
        Keybinds.NumpadDigits.Value = (bool)Keybinds.NumpadDigits.DefaultValue;
        Notify?.Invoke("All keybinds reset to default", false);
    }

    /// <summary>Touche pressée cette frame (hors touches spéciales), sinon Key.None.</summary>
    static Key PressedKey(Keyboard kb)
    {
        if (!kb.anyKey.wasPressedThisFrame) return Key.None;
        allKeys ??= ((Key[])Enum.GetValues(typeof(Key))).Where(k => k != Key.None && k != Key.IMESelected).Distinct().ToArray();
        foreach (var k in allKeys)
        {
            try { if (kb[k].wasPressedThisFrame) return k; }
            catch { } // touche absente de ce clavier
        }
        return Key.None;
    }

    void CaptureKey()
    {
        var bind = Keybinds.Capturing;
        var kb = Keyboard.current;
        if (bind == null || kb == null) return;
        var k = PressedKey(kb);
        if (k == Key.None) return;
        Keybinds.Capturing = null;
        if (k == Key.Escape) return;
        if (k == Key.Backspace || k == Key.Delete)
        {
            bind.Set("");
            Notify?.Invoke($"{bind.Name}: no key", false);
            return;
        }
        var others = Keybinds.UsersOf(k, bind).Select(b => b.Name).ToList();
        bind.Set(k.ToString());
        Notify?.Invoke(others.Count == 0 ? $"{bind.Name}: {k}" : $"{bind.Name}: {k} (also used by {string.Join(", ", others)})", others.Count > 0);
    }

    void UpdateKeybinds(bool hasMouse, float mx, float my, bool click, bool rightClick, ref bool handled)
    {
        CaptureKey();
        // Clic ailleurs pendant la saisie : annule
        if (Keybinds.Capturing != null && click) Keybinds.Capturing = null;

        string title = "Keybinds", text = "Options: click to switch on / off. Shortcuts: click one, then press the new key. Backspace: no key. Esc: cancel. Right-click: back to default. Changes are saved at once.";

        foreach (var (btn, x, y, w, h) in keyButtons)
        {
            bool hover = hasMouse && mx >= x && mx < x + w && my >= y && my < y + h;
            btn.SetHover(hover);
            if (hover) { title = "Reset all to default"; text = "Puts every shortcut back to its default key."; }
            if (click && hover && !handled) { handled = true; btn.OnClick?.Invoke(); }
        }

        foreach (var r in keyRows)
        {
            bool hover = hasMouse && my >= r.y && my < r.y + KeyRowH && mx >= 12 && mx < W - 14;
            bool capturing = r.bind != null && Keybinds.Capturing == r.bind;
            Ui.SetActive(r.highlight, hover || capturing);

            string chip;
            bool custom;
            if (r.option != null)
            {
                var entry = r.option.Entry();
                bool on = entry?.Value ?? false;
                chip = on ? "On" : "Off";
                custom = entry != null && on != (bool)entry.DefaultValue;
                if (hover)
                {
                    title = r.option.Name;
                    text = $"{r.option.Help}  Default: {(entry != null && (bool)entry.DefaultValue ? "On" : "Off")}.  Click: switch on / off. Right-click: default.";
                }
                if (entry != null && hover && (click || rightClick) && !handled)
                {
                    handled = true;
                    Keybinds.Capturing = null;
                    entry.Value = click ? !on : (bool)entry.DefaultValue;
                    Notify?.Invoke($"{r.option.Name}: {(entry.Value ? "on" : "off")}", false);
                }
            }
            else if (r.bind == null)
            {
                bool numpad = Keybinds.NumpadDigits.Value;
                chip = numpad ? "Numpad 0-9" : "Top row 0-9";
                custom = numpad != (bool)Keybinds.NumpadDigits.DefaultValue;
                if (hover)
                {
                    title = "Slot digits";
                    text = "Keys used to pick a slot (0-9) after Save to slot, Load slot, etc. Click: switch between the top row digits and the numpad.";
                }
                if (hover && click && !handled)
                {
                    handled = true;
                    Keybinds.NumpadDigits.Value = !numpad;
                    Notify?.Invoke($"Slot digits: {(!numpad ? "numpad" : "top row")}", false);
                }
            }
            else
            {
                chip = capturing ? "Press a key…" : r.bind.Label;
                custom = !r.bind.IsDefault;
                if (hover)
                {
                    title = r.bind.Name;
                    text = $"{r.bind.Help}  Default: {Bind.LabelOf(r.bind.DefaultKeys)}.  Click, then press a key. Backspace: no key. Esc: cancel. Right-click: default.";
                }
                if (hover && click && !handled) { handled = true; Keybinds.Capturing = r.bind; }
                if (hover && rightClick && !handled)
                {
                    handled = true;
                    Keybinds.Capturing = null;
                    r.bind.ResetToDefault();
                    Notify?.Invoke($"{r.bind.Name}: back to {Bind.LabelOf(r.bind.DefaultKeys)}", false);
                }
            }

            Ui.SetText(r.chip, chip);
            r.chip.color = capturing ? Theme.GoldText : chip == "—" || (r.option != null && chip == "Off") ? Theme.TextDim : Theme.KeyText;
            r.chipFill.color = capturing ? Theme.Crimson : Theme.BgButton;
            Ui.SetBorder(r.chipFill, capturing ? Theme.GoldText : custom ? Theme.GoldBright : Theme.Gold);
            r.name.color = capturing ? Theme.GoldText : custom ? Theme.GoldLabel : Theme.Text;
        }

        Ui.SetText(detailTitle, title);
        Ui.SetText(detailText, text);
    }
}
