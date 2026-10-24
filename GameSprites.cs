using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BelmontPractice;

/// <summary>
/// Icônes livrées avec le mod (dossier icons/ à côté de la DLL), extraites une fois des visuels du jeu :
/// cœurs de vie / mana (menu Progression) et livres Attack+ / Defense+ (rendu de leur modèle 3D, sans icône 2D dans le jeu).
/// </summary>
public static class GameSprites
{
    public const string AttackBook = "attack_book", DefenseBook = "defense_book", LifeHeart = "life_heart", ManaHeart = "mana_heart";

    static readonly Dictionary<string, Sprite> cache = new();

    public static Sprite Icon(string name) => Icon(name, Vector4.zero);

    /// <summary>Icône avec bords fixes pour un affichage découpé en 9 (gauche, bas, droite, haut, en pixels).</summary>
    public static Sprite Icon(string name, Vector4 border)
    {
        if (cache.TryGetValue(name, out var s)) return s;
        try
        {
            var dir = Path.GetDirectoryName(typeof(GameSprites).Assembly.Location) ?? "";
            var path = Path.Combine(dir, "icons", name + ".png");
            if (File.Exists(path))
            {
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                ImageConversion.LoadImage(tex, File.ReadAllBytes(path));
                tex.wrapMode = TextureWrapMode.Clamp;
                tex.hideFlags = HideFlags.HideAndDontSave;
                s = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
                s.hideFlags = HideFlags.HideAndDontSave;
            }
            else Plugin.Logger.LogWarning("[Icônes] introuvable : " + path);
        }
        catch (Exception ex) { Plugin.Logger.LogWarning($"[Icônes] {name} : {ex.Message}"); }
        return cache[name] = s;
    }
}
