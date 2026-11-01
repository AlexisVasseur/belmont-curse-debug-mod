using UnityEngine;

namespace BelmontPractice;

/// <summary>
/// Fondu d'ouverture / de fermeture d'une fenêtre du mod (CanvasGroup sur sa racine), en temps réel :
/// le jeu peut être en pause.
/// </summary>
public sealed class Fade
{
    const float InTime = 0.15f, OutTime = 0.12f;
    // Une frame lente (construction de la fenêtre, chargement) ne doit pas sauter le fondu
    const float MaxStep = 1f / 30f;

    GameObject target;
    CanvasGroup group;
    float t; // 0 : invisible, 1 : opaque

    /// <summary>Fenêtre ouverte : l'active et la fait apparaître.</summary>
    public void In(GameObject go)
    {
        if (go == null) return;
        if (!go.activeSelf) go.SetActive(true);
        Step(go, InTime);
    }

    /// <summary>Fenêtre fermée : la fait disparaître puis la désactive.</summary>
    public void Out(GameObject go)
    {
        if (go == null || !go.activeSelf) { t = 0f; return; }
        Step(go, -OutTime);
        if (t <= 0f) go.SetActive(false);
    }

    void Step(GameObject go, float duration)
    {
        if (target != go || group == null)
        {
            target = go;
            group = go.GetComponent<CanvasGroup>() ?? go.AddComponent<CanvasGroup>();
        }
        t = Mathf.Clamp01(t + Mathf.Min(Time.unscaledDeltaTime, MaxStep) / duration);
        group.alpha = t * t * (3f - 2f * t); // départ et arrivée en douceur
    }
}
