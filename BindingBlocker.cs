using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BelmontPractice;

/// <summary>
/// Désactive les liaisons des actions du jeu qui correspondent à un filtre (chemin de remplacement vide),
/// puis les rétablit une fois le blocage levé et les touches relâchées, pour que le jeu ne voie aucun appui.
/// </summary>
public class BindingBlocker
{
    struct Saved { public InputAction action; public int index; public string overridePath; }

    readonly string tag;
    readonly Func<string, bool> matches;
    readonly Func<bool> anyHeld;
    readonly List<Saved> saved = new();
    bool blocked;
    float nextRescan;

    public BindingBlocker(string tag, Func<string, bool> matches, Func<bool> anyHeld)
    {
        this.tag = tag;
        this.matches = matches;
        this.anyHeld = anyHeld;
    }

    public void Update(bool block)
    {
        try
        {
            if (block)
            {
                if (!blocked || Time.unscaledTime >= nextRescan) Block(); // rescan : actions activées entre-temps
                return;
            }
            // Touche maintenue au moment du déblocage : on attend le relâchement
            if (blocked && !anyHeld()) Unblock();
        }
        catch (Exception ex) { Plugin.Logger.LogWarning($"[{tag}] " + ex.Message); }
    }

    bool AlreadySaved(InputAction a, int i)
    {
        foreach (var s in saved) if (s.index == i && s.action.Pointer == a.Pointer) return true;
        return false;
    }

    void Block()
    {
        blocked = true;
        nextRescan = Time.unscaledTime + 0.5f;
        var actions = InputSystem.ListEnabledActions();
        for (int n = 0; n < actions.Count; n++)
        {
            var a = actions[n];
            if (a == null) continue;
            var bindings = a.bindings;
            for (int i = 0; i < bindings.Count; i++)
            {
                var b = bindings[i];
                if (b.isComposite || !matches(b.effectivePath) || AlreadySaved(a, i)) continue;
                saved.Add(new Saved { action = a, index = i, overridePath = b.overridePath });
                a.ApplyBindingOverride(i, ""); // chemin vide = liaison désactivée
            }
        }
    }

    void Unblock()
    {
        foreach (var s in saved)
        {
            try
            {
                if (s.overridePath == null) s.action.RemoveBindingOverride(s.index);
                else s.action.ApplyBindingOverride(s.index, s.overridePath);
            }
            catch (Exception ex) { Plugin.Logger.LogWarning($"[{tag}] rétablissement : " + ex.Message); }
        }
        saved.Clear();
        blocked = false;
    }
}

/// <summary>
/// Pendant le renommage d'une savestate ou la saisie d'une touche dans l'onglet Keybinds,
/// les liaisons clavier des actions du jeu sont désactivées : taper un nom ne fait ni bouger ni attaquer le personnage.
/// </summary>
public static class KeyboardBlock
{
    static readonly BindingBlocker blocker = new("Clavier",
        path => !string.IsNullOrEmpty(path) && path.StartsWith("<Keyboard>", StringComparison.OrdinalIgnoreCase),
        () => Keyboard.current != null && Keyboard.current.anyKey.isPressed);

    public static void Update(bool block) => blocker.Update(block);
}
