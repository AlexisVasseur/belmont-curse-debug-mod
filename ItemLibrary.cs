using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace BelmontPractice;

/// <summary>
/// Charge tous les objets de la bibliothèque du jeu (ItemsManager._itemsLibrary), pour que les armes et
/// sorts pas encore rencontrés soient aussi en mémoire. Chargement asynchrone avec nos propres requêtes
/// (jamais via les AssetReference du jeu, qui ne peuvent être chargées qu'une fois).
/// </summary>
public static class ItemLibrary
{
    static readonly List<AsyncOperationHandle<ItemData>> pending = new();
    public static readonly List<ItemData> Loaded = new();
    static bool started;
    static float startTime;
    public static bool Done { get; private set; }

    public static void Update()
    {
        if (Done) return;
        try
        {
            if (!started)
            {
                var o = UnityEngine.Object.FindObjectOfType(Il2CppType.Of<ItemsManager>());
                var lib = o?.Cast<ItemsManager>()?._itemsLibrary;
                if (lib == null) return; // pas encore en jeu
                started = true;
                startTime = Time.unscaledTime;
                foreach (var r in lib.elementRefs)
                {
                    if (r == null) continue;
                    var asset = r.Asset; // déjà chargé par le jeu
                    if (asset != null) { var it = asset.TryCast<ItemData>(); if (it != null) Loaded.Add(it); continue; }
                    if (!r.RuntimeKeyIsValid()) continue;
                    pending.Add(Addressables.LoadAssetAsync<ItemData>(r.RuntimeKey));
                }
                Plugin.Logger.LogInfo($"[Objets] bibliothèque : {lib.elementRefs.Length} objets, {pending.Count} à charger");
            }

            for (int i = pending.Count - 1; i >= 0; i--)
            {
                var h = pending[i];
                if (!h.IsDone) continue;
                if (h.Status == AsyncOperationStatus.Succeeded && h.Result != null) Loaded.Add(h.Result);
                pending.RemoveAt(i);
            }
            if (pending.Count == 0 || Time.unscaledTime - startTime > 20f)
            {
                Done = true;
                Plugin.Logger.LogInfo($"[Objets] bibliothèque chargée : {Loaded.Count} objets ({pending.Count} non chargés)");
            }
        }
        catch (Exception ex)
        {
            Done = true;
            Plugin.Logger.LogWarning("[Objets] bibliothèque : " + ex.Message);
        }
    }
}
