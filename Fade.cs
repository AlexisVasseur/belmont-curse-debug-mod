using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BelmontPractice;

/// <summary>
/// Fondu d'ouverture / de fermeture d'une fenêtre du mod (CanvasGroup sur sa racine), en temps réel :
/// le jeu peut être en pause. Toute la fenêtre a la même opacité, boutons compris : leurs contours dorés
/// (copies dessinées sous le fond) sont coupés tant qu'elle est transparente, sinon ils se voient à travers et le bouton
/// paraît doré.
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
    readonly List<Shadow> muted = new(); // contours coupés pendant le fondu

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
            muted.Clear(); // fenêtre reconstruite : ses contours sont neufs
        }
        float step = duration <= 0f ? 1f : Mathf.Min(Time.unscaledDeltaTime, MaxStep) / duration;
        t = Mathf.Clamp01(appear ? t + step : t - step);
        group.alpha = t * t * (3f - 2f * t); // départ et arrivée en douceur
        MuteOutlines(go, group.alpha < 1f);
    }

    void MuteOutlines(GameObject go, bool mute)
    {
        if (!mute)
        {
            foreach (var s in muted) if (s != null) s.enabled = true;
            muted.Clear();
            return;
        }
        if (muted.Count > 0) return;
        foreach (var s in go.GetComponentsInChildren<Shadow>(true))
            if (s.enabled) { s.enabled = false; muted.Add(s); }
    }
}
