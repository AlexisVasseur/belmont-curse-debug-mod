using System;
using UnityEngine;
using UnityEngine.UI;

namespace BelmontPractice;

/// <summary>
/// Écran noir plein écran, dessiné avec un Canvas uGUI au-dessus de tous ceux du jeu.
/// (GUI.DrawTexture n'est pas utilisable : la méthode a été retirée du jeu compilé et
/// Il2CppInterop échoue à la reconstruire — « Method unstripping failed ».)
/// </summary>
public static class FadeOverlay
{
    static GameObject root;
    static Image image;
    static bool failed;
    static float current = -1f;

    static bool Ensure()
    {
        if (image != null && root != null) return true;
        if (failed) return false;
        try
        {
            root = new GameObject("BelmontPractice_FonduNoir");
            UnityEngine.Object.DontDestroyOnLoad(root);
            var canvas = root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue - 1; // au-dessus de toutes les interfaces du jeu ; seules les versions du mod passent devant

            var child = new GameObject("Noir");
            child.transform.SetParent(root.transform, false);
            image = child.AddComponent<Image>();
            image.raycastTarget = false;
            var rt = image.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.offsetMin = new Vector2(0f, 0f);
            rt.offsetMax = new Vector2(0f, 0f);
            image.color = new Color(0f, 0f, 0f, 0f);
            root.SetActive(false);
            Plugin.Logger.LogInfo("[Fondu] écran noir uGUI créé");
            return true;
        }
        catch (Exception ex)
        {
            failed = true;
            Plugin.Logger.LogError("[Fondu] impossible de créer l'écran noir : " + ex);
            return false;
        }
    }

    /// <summary>Opacité de l'écran noir (0 = invisible, 1 = noir complet).</summary>
    public static void Set(float alpha)
    {
        alpha = Mathf.Clamp01(alpha);
        if (alpha == current && root != null) return;
        if (!Ensure()) return;
        try
        {
            if (alpha <= 0f)
            {
                root.SetActive(false);
            }
            else
            {
                image.color = new Color(0f, 0f, 0f, alpha);
                root.SetActive(true);
            }
            current = alpha;
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogError("[Fondu] " + ex.Message);
            image = null; // sera recréé
            current = -1f;
        }
    }
}
