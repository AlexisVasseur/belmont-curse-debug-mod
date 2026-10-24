using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BelmontPractice;

/// <summary>
/// Empêche les clics et la molette sur les fenêtres du mod d'agir sur le jeu (attaque, etc.) :
/// tant que la souris survole une fenêtre, les liaisons souris des actions du jeu sont désactivées
/// (chemin de remplacement vide), puis rétablies une fois la souris sortie et les boutons relâchés.
/// </summary>
public static class MouseBlock
{
    struct Saved { public InputAction action; public int index; public string overridePath; }

    static readonly List<Saved> saved = new();
    static bool blocked;
    static float nextRescan;

    public static void Update(bool overUi)
    {
        try
        {
            if (overUi)
            {
                if (!blocked || Time.unscaledTime >= nextRescan) Block(); // rescan : actions activées entre-temps
                return;
            }
            // Bouton maintenu en sortant de la fenêtre : on attend le relâchement pour que le jeu ne voie pas d'appui
            if (blocked && !AnyButtonHeld()) Unblock();
        }
        catch (Exception ex) { Plugin.Logger.LogWarning("[Souris] " + ex.Message); }
    }

    static bool AnyButtonHeld()
    {
        var m = Mouse.current;
        return m != null && (m.leftButton.isPressed || m.rightButton.isPressed || m.middleButton.isPressed);
    }

    static bool IsMouseButtonOrWheel(string path)
    {
        if (string.IsNullOrEmpty(path)) return false;
        if (!path.StartsWith("<Mouse>", StringComparison.OrdinalIgnoreCase) && !path.StartsWith("<Pointer>", StringComparison.OrdinalIgnoreCase)) return false;
        return path.IndexOf("Button", StringComparison.OrdinalIgnoreCase) >= 0
            || path.IndexOf("press", StringComparison.OrdinalIgnoreCase) >= 0
            || path.IndexOf("scroll", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    static bool AlreadySaved(InputAction a, int i)
    {
        foreach (var s in saved) if (s.index == i && s.action.Pointer == a.Pointer) return true;
        return false;
    }

    static void Block()
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
                if (b.isComposite || !IsMouseButtonOrWheel(b.effectivePath) || AlreadySaved(a, i)) continue;
                saved.Add(new Saved { action = a, index = i, overridePath = b.overridePath });
                a.ApplyBindingOverride(i, ""); // chemin vide = liaison désactivée
            }
        }
    }

    static void Unblock()
    {
        foreach (var s in saved)
        {
            try
            {
                if (s.overridePath == null) s.action.RemoveBindingOverride(s.index);
                else s.action.ApplyBindingOverride(s.index, s.overridePath);
            }
            catch (Exception ex) { Plugin.Logger.LogWarning("[Souris] rétablissement : " + ex.Message); }
        }
        saved.Clear();
        blocked = false;
    }

    /// <summary>La souris est-elle au-dessus d'un élément visible du mod (fenêtres, panneau, boutons) ?</summary>
    public static bool PointerOverModUi()
    {
        var root = UiCanvas.Root;
        var mouse = Mouse.current;
        if (root == null || mouse == null || !root.activeInHierarchy) return false;
        Vector2 pos = mouse.position.ReadValue();
        foreach (var img in root.GetComponentsInChildren<Image>(false))
        {
            if (!img.enabled || img.color.a <= 0.01f) continue;
            if (RectTransformUtility.RectangleContainsScreenPoint(img.rectTransform, pos, null)) return true;
        }
        return false;
    }
}
