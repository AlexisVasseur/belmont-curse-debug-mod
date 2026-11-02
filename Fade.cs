using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BelmontPractice;

/// <summary>
/// Fondu d'ouverture / de fermeture d'une fenêtre du mod (CanvasGroup sur sa racine), en temps réel :
/// le jeu peut être en pause. Toute la fenêtre a la même opacité, boutons compris. Leurs contours dorés
/// sont des copies dessinées sous le fond : à la même opacité, elles se verraient à travers et le bouton paraîtrait doré.
/// Ils suivent donc une courbe plus raide (opacité à la puissance 4) : présents en même temps que la fenêtre, à peine
/// visibles tant qu'elle est transparente.
/// </summary>
/// <param name="inTime">Durée de l'apparition ; 0 : instantanée.</param>
/// <param name="outTime">Durée de la disparition ; 0 : instantanée.</param>
public sealed class Fade(float inTime = 0.15f, float outTime = 0.12f)
{
    // Une frame lente (construction de la fenêtre, chargement) ne doit pas sauter le fondu
    const float MaxStep = 1f / 30f;

    GameObject target;
    CanvasGroup group;
    float t; // 0 : invisible, 1 : opaque
    readonly List<(Shadow s, float a)> outlines = new(); // contours de la fenêtre et leur opacité d'origine
    bool outlinesFaded;

    /// <summary>Fenêtre ouverte : l'active et la fait apparaître.</summary>
    public void In(GameObject go)
    {
        if (go == null) return;
        if (!go.activeSelf) go.SetActive(true);
        Step(go, true, inTime);
    }

    /// <summary>Fenêtre fermée : la fait disparaître puis la désactive.</summary>
    public void Out(GameObject go)
    {
        if (go == null || !go.activeSelf) { t = 0f; return; }
        Step(go, false, outTime);
        if (t <= 0f) go.SetActive(false);
    }

    void Step(GameObject go, bool appear, float duration)
    {
        if (target != go || group == null)
        {
            target = go;
            group = go.GetComponent<CanvasGroup>() ?? go.AddComponent<CanvasGroup>();
            outlines.Clear(); // fenêtre reconstruite : ses contours sont neufs
            outlinesFaded = false;
        }
        float step = duration <= 0f ? 1f : Mathf.Min(Time.unscaledDeltaTime, MaxStep) / duration;
        t = Mathf.Clamp01(appear ? t + step : t - step);
        group.alpha = t * t * (3f - 2f * t); // départ et arrivée en douceur
        FadeOutlines(go, group.alpha);
    }

    void FadeOutlines(GameObject go, float alpha)
    {
        if (alpha >= 1f && !outlinesFaded) return;
        if (!outlinesFaded)
        {
            outlines.Clear();
            foreach (var s in go.GetComponentsInChildren<Shadow>(true)) outlines.Add((s, s.effectColor.a));
        }
        float k = alpha * alpha * alpha * alpha;
        foreach (var (s, a) in outlines)
        {
            if (s == null) continue;
            var c = s.effectColor;
            c.a = a * k;
            s.effectColor = c;
        }
        outlinesFaded = alpha < 1f;
    }
}
