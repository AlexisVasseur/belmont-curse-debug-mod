using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BelmontPractice;

/// <summary>Canvas commun à toutes les fenêtres du mod (savestates, panneau latéral, messages).</summary>
public static class UiCanvas
{
    public static GameObject Root { get; private set; }
    public static Canvas Canvas { get; private set; }

    public static void Ensure()
    {
        if (Root != null) return;
        Root = new GameObject("BelmontPractice_UI");
        Object.DontDestroyOnLoad(Root);
        Canvas = Root.AddComponent<Canvas>();
        Canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        Canvas.sortingOrder = 32000; // sous l'écran noir des chargements (32767)
        var scaler = Root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        // Échelle de l'interface : 0.5 = moitié de la taille « 1080p »
        float uiScale = Mathf.Clamp(Plugin.UiScale.Value, 0.2f, 2f);
        scaler.referenceResolution = new Vector2(1920f / uiScale, 1080f / uiScale);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
    }

    /// <summary>Largeur du canvas en unités de référence.</summary>
    public static float Width => Canvas != null && Canvas.scaleFactor > 0f ? Screen.width / Canvas.scaleFactor : 1920f;

    /// <summary>Hauteur du canvas en unités de référence.</summary>
    public static float Height => Canvas != null && Canvas.scaleFactor > 0f ? Screen.height / Canvas.scaleFactor : 1080f;

    /// <summary>Position de la souris en unités de référence, origine en haut à gauche de l'écran.</summary>
    public static bool Mouse(out float x, out float y)
    {
        x = y = 0f;
        var mouse = UnityEngine.InputSystem.Mouse.current;
        if (mouse == null || Canvas == null) return false;
        float scale = Canvas.scaleFactor;
        if (scale <= 0f) return false;
        Vector2 pos = mouse.position.ReadValue();
        x = pos.x / scale;
        y = (Screen.height - pos.y) / scale;
        return true;
    }

    public static bool RightClicked => UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.rightButton.wasPressedThisFrame;

    public static bool Clicked => UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame;
}
