using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using UnityEngine.InputSystem;

namespace BelmontPractice;

/// <summary>Un raccourci configurable : une ou plusieurs touches séparées par des virgules (ex. "F5,Numpad7").</summary>
public class Bind
{
    readonly ConfigEntry<string> entry;
    List<Key> keys = new();
    string parsedFrom;

    /// <summary>Nom et explication affichés dans l'onglet Keybinds (en anglais).</summary>
    public readonly string Name, Help, Section;

    public Bind(ConfigFile config, string key, string defaultKeys, string description, string section, string name, string help)
    {
        entry = config.Bind("Raccourcis", key, defaultKeys,
            description + " — touches séparées par des virgules, noms de UnityEngine.InputSystem.Key (F5, Numpad7, Digit1…). Vide = aucun.");
        Section = section;
        Name = name;
        Help = help;
        Keybinds.All.Add(this);
    }

    void Parse()
    {
        if (parsedFrom == entry.Value) return;
        parsedFrom = entry.Value;
        keys = new List<Key>();
        foreach (var part in (entry.Value ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Enum.TryParse(part, true, out Key k) && k != Key.None) keys.Add(k);
            else Plugin.Logger.LogWarning($"Raccourci '{entry.Definition.Key}' : touche inconnue '{part}'");
        }
    }

    public bool Pressed(Keyboard kb)
    {
        Parse();
        foreach (var k in keys)
            if (kb[k].wasPressedThisFrame) return true;
        return false;
    }

    public bool Uses(Key k) { Parse(); return keys.Contains(k); }

    /// <summary>Remplace les touches (enregistré aussitôt dans le fichier de config). Vide = aucune touche.</summary>
    public void Set(string value) => entry.Value = value ?? "";

    public string DefaultKeys => (string)entry.DefaultValue ?? "";
    public bool IsDefault => (entry.Value ?? "") == DefaultKeys;
    public void ResetToDefault() => Set(DefaultKeys);

    /// <summary>Texte court pour l'interface : "F5 / Numpad7".</summary>
    public string Label { get { Parse(); return keys.Count == 0 ? "—" : string.Join(" / ", keys.Select(k => k.ToString())); } }

    public static string LabelOf(string keysText)
    {
        var parts = (keysText ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 0 ? "none" : string.Join(" / ", parts);
    }

    /// <summary>Première touche, en version courte pour une pastille : Numpad9 → "9", F5 → "F5".</summary>
    public string ShortLabel
    {
        get
        {
            Parse();
            if (keys.Count == 0) return "";
            string s = keys[0].ToString();
            if (s.StartsWith("Numpad") && s.Length == 7) return s.Substring(6);
            if (s.StartsWith("Digit") && s.Length == 6) return s.Substring(5);
            return s;
        }
    }

    public bool HasKeys { get { Parse(); return keys.Count > 0; } }
}

public static class Keybinds
{
    public const string SectionSavestates = "Savestates", SectionUtilities = "Utilities";

    /// <summary>Tous les raccourcis, dans l'ordre de l'onglet Keybinds.</summary>
    public static readonly List<Bind> All = new();

    /// <summary>Raccourci en cours de modification dans l'onglet Keybinds : les raccourcis sont alors ignorés.</summary>
    public static Bind Capturing;

    public static Bind TogglePanel, QuickSave, QuickLoad, QuickslotToFile, FileToQuickslot,
        SaveToFile, LoadFromFile, NextPage, PrevPage, RefreshFiles;
    public static Bind ToggleInfiniteHealth, ToggleInfiniteMana, ToggleInfinitePotions, ToggleInfiniteJump, ToggleNoclip, MapTeleport, FastForward;

    internal static ConfigEntry<bool> NumpadDigits;

    public static void Init(ConfigFile c)
    {
        const string S = SectionSavestates, U = SectionUtilities;
        // Valeurs par défaut reprises des raccourcis DebugMod de Silksong (pavé numérique).
        TogglePanel = new Bind(c, "AfficherPanneau", "F1", "Affiche / masque le panneau des savestates", S,
            "Toggle panel", "Shows or hides the mod panels.");
        QuickSave = new Bind(c, "SauverEtat", "F5,Numpad7", "Sauve l'état actuel dans le quickslot", S,
            "Save quickslot", "Saves the current state into the quickslot.");
        QuickLoad = new Bind(c, "ChargerEtat", "F6,Numpad8", "Charge le quickslot", S,
            "Load quickslot", "Loads the state stored in the quickslot.");
        SaveToFile = new Bind(c, "SauverDansSlot", "Numpad9", "Sauve l'état actuel dans un emplacement (puis choisir 0-9)", S,
            "Save to slot", "Saves the current state into a slot of the current page: press the key, then a digit 0-9.");
        LoadFromFile = new Bind(c, "ChargerSlot", "Numpad3", "Charge un emplacement (puis choisir 0-9)", S,
            "Load slot", "Loads a slot of the current page: press the key, then a digit 0-9.");
        FileToQuickslot = new Bind(c, "SlotVersQuickslot", "Numpad2", "Copie un emplacement dans le quickslot (puis choisir 0-9)", S,
            "Slot to quickslot", "Copies a slot into the quickslot: press the key, then a digit 0-9.");
        QuickslotToFile = new Bind(c, "QuickslotVersSlot", "", "Copie le quickslot dans un emplacement (puis choisir 0-9)", S,
            "Quickslot to slot", "Copies the quickslot into a slot: press the key, then a digit 0-9.");
        NextPage = new Bind(c, "PageSuivante", "Numpad6", "Page de savestates suivante", S,
            "Next page", "Goes to the next savestate page.");
        PrevPage = new Bind(c, "PagePrecedente", "Numpad4", "Page de savestates précédente", S,
            "Previous page", "Goes to the previous savestate page.");
        RefreshFiles = new Bind(c, "RelireFichiers", "", "Relit les savestates depuis le disque", S,
            "Reload files", "Reads the savestate files from disk again (after editing them by hand).");

        ToggleInfiniteHealth = new Bind(c, "BasculerPVInfinis", "", "Active / désactive les PV infinis", U,
            "Infinite HP", "Turns infinite HP on or off.");
        ToggleInfiniteMana = new Bind(c, "BasculerManaInfinie", "", "Active / désactive la mana infinie", U,
            "Infinite mana", "Turns infinite mana on or off.");
        ToggleInfinitePotions = new Bind(c, "BasculerPotionsInfinies", "", "Active / désactive les potions infinies", U,
            "Infinite potions", "Turns infinite potions on or off.");
        ToggleInfiniteJump = new Bind(c, "BasculerSautsInfinis", "", "Active / désactive les sauts infinis", U,
            "Infinite jump", "Turns infinite jump on or off.");
        ToggleNoclip = new Bind(c, "BasculerNoclip", "", "Active / désactive le noclip", U,
            "Noclip", "Turns noclip on or off.");

        MapTeleport = new Bind(c, "TeleportCarte", "T", "Carte ouverte : choisit le point sous le curseur comme destination de téléportation", U,
            "Map teleport", "With the map open: asks to teleport to the point under the cursor (also middle-click or Start on a gamepad); press again to go.");

        FastForward = new Bind(c, "AccelererCinematiques", "F7", "Pendant une cinématique : l'accélère (vitesse réglable, Utilitaires/VitesseCinematiques), ainsi que les suivantes ; les vidéos sont passées comme par la touche Passer du jeu", U,
            "Fast-forward cutscenes", "During a cutscene: plays it 10× faster (adjustable in the config), and the cutscenes that follow too (boss outro, absorption…). Videos are skipped like holding the game's skip button. Gameplay always runs at normal speed. Press again to stop. Gamepad: Y.");

        NumpadDigits = c.Bind("Raccourcis", "ChiffresPaveNumerique", false,
            "Choix de l'emplacement (0-9) avec le pavé numérique au lieu des chiffres du haut du clavier");
    }

    /// <summary>Autres raccourcis qui utilisent déjà cette touche.</summary>
    public static IEnumerable<Bind> UsersOf(Key k, Bind except) => All.Where(b => b != except && b.Uses(k));

    /// <summary>Chiffre 0-9 pressé cette frame pour choisir un emplacement, sinon -1.</summary>
    public static int DigitPressed(Keyboard kb, out Key key)
    {
        for (int i = 0; i <= 9; i++)
        {
            // Attention : dans l'enum, Digit0 vient APRÈS Digit9 (Digit1 = 41 … Digit9 = 49, Digit0 = 50).
            key = NumpadDigits.Value ? Key.Numpad0 + i : (i == 0 ? Key.Digit0 : Key.Digit1 + (i - 1));
            if (kb[key].wasPressedThisFrame) return i;
        }
        key = Key.None;
        return -1;
    }
}
