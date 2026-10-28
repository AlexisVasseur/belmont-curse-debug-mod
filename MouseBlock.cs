using System;
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
    static readonly BindingBlocker blocker = new("Souris", IsMouseButtonOrWheel, AnyButtonHeld);

    public static void Update(bool overUi) => blocker.Update(overUi);

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
