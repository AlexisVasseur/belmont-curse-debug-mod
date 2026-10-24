using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace BelmontPractice;

/// <summary>
/// Icônes des objets du jeu (ItemData.itemDisplayIconAtlased / itemDisplayIconRef).
/// On ne charge jamais via l'AssetReference du jeu (une référence ne peut être chargée qu'une fois :
/// le menu du jeu échouerait ensuite) : soit l'icône est déjà chargée par le jeu, soit on la charge
/// à part avec Addressables.LoadAssetAsync&lt;Sprite&gt;(clé).
/// </summary>
public static class ItemIcons
{
    class Entry
    {
        public AsyncOperationHandle<Sprite> handle;
        public bool started, failed;
        public Sprite sprite;
        public List<Il2CppSystem.Object> keys; // clés à essayer dans l'ordre
        public int keyIndex;
    }

    /// <summary>Clés possibles : sprite atlasé (guid[sous-objet]), sprite simple, puis l'asset principal seul.</summary>
    static List<Il2CppSystem.Object> KeysFor(ItemData item)
    {
        var keys = new List<Il2CppSystem.Object>();
        void Add(AssetReference r)
        {
            if (r == null) return;
            try { if (r.RuntimeKeyIsValid()) keys.Add(r.RuntimeKey); } catch { }
            try
            {
                string guid = r.AssetGUID;
                if (!string.IsNullOrEmpty(guid))
                {
                    string sub = r.SubObjectName;
                    if (!string.IsNullOrEmpty(sub)) keys.Add((Il2CppSystem.Object)(Il2CppSystem.String)$"{guid}[{sub}]");
                    keys.Add((Il2CppSystem.Object)(Il2CppSystem.String)guid);
                }
            }
            catch { }
        }
        Add(item.itemDisplayIconAtlased);
        Add(item.itemDisplayIconRef);
        return keys;
    }

    static readonly Dictionary<string, Entry> cache = new();

    /// <summary>Retourne l'icône si elle est disponible (sinon lance le chargement et retourne null).</summary>
    public static Sprite Get(ItemData item)
    {
        if (item == null) return null;
        string key = item.name;
        if (!cache.TryGetValue(key, out var e)) cache[key] = e = new Entry();
        if (e.sprite != null || e.failed) return e.sprite;

        try
        {
            if (!e.started)
            {
                // Déjà chargée par le jeu ?
                foreach (AssetReference r in new AssetReference[] { item.itemDisplayIconAtlased, item.itemDisplayIconRef })
                {
                    var loaded = r?.Asset;
                    if (loaded == null) continue;
                    e.sprite = loaded.TryCast<Sprite>();
                    if (e.sprite != null) return e.sprite;
                }
                e.keys = KeysFor(item);
                e.keyIndex = 0;
                if (e.keys.Count == 0) { e.failed = true; Plugin.Logger.LogInfo($"[Icônes] pas d'icône pour {key}"); return null; }
                e.handle = Addressables.LoadAssetAsync<Sprite>(e.keys[0]);
                e.started = true;
                return null;
            }

            if (!e.handle.IsDone) return null;
            if (e.handle.Status == AsyncOperationStatus.Succeeded && e.handle.Result != null) { e.sprite = e.handle.Result; return e.sprite; }

            // Échec : clé suivante
            Plugin.Logger.LogInfo($"[Icônes] {key} : échec avec la clé '{e.keys[e.keyIndex]?.ToString()}'");
            e.keyIndex++;
            if (e.keyIndex < e.keys.Count) e.handle = Addressables.LoadAssetAsync<Sprite>(e.keys[e.keyIndex]);
            else { e.failed = true; Plugin.Logger.LogWarning($"[Icônes] aucune icône chargeable pour {key}"); }
        }
        catch (Exception ex)
        {
            e.failed = true;
            Plugin.Logger.LogWarning($"[Icônes] {key} : {ex.Message}");
        }
        return e.sprite;
    }
}
