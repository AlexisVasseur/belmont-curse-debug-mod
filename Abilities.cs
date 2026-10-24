using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using MotherBase.EntityFramework;
using UnityEngine;

namespace BelmontPractice;

/// <summary>
/// Compétences de « Parcours » (VaniaMoves) : Whip, VaniaLight, DoubleJump, AerialDash, Stomp.
/// Chacune est un asset VaniaMoveID ; le VaniaMovesComponent du joueur les verrouille / déverrouille.
/// </summary>
public static class Abilities
{
    public class Move
    {
        public string Key;          // nom de l'asset sans suffixe, ex. "DoubleJump"
        public string Fallback;     // nom de secours si le jeu ne fournit pas le sien
        // Nom officiel du jeu (en anglais) dès que l'objet correspondant est chargé
        public string DisplayName => Item != null ? Loc.Get(Item.itemDisplayName, Fallback) : Fallback;
        public VaniaMoveID Id;      // null si l'asset n'est pas chargé
        public VaniaMoveItemData Item; // objet d'inventaire correspondant (pour l'icône), peut être null
    }

    // Ordre et noms du menu « Parcours » du jeu
    public static readonly List<Move> Traversal = new()
    {
        new Move { Key = "Whip", Fallback = "Arcane Whip" },
        new Move { Key = "VaniaLight", Fallback = "Sacred Flame" },
        new Move { Key = "DoubleJump", Fallback = "Double Jump" },
        new Move { Key = "AerialDash", Fallback = "Air Dash" },
        new Move { Key = "Stomp", Fallback = "Stomp" },
    };

    static float nextResolve;

    /// <summary>Retrouve les assets VaniaMoveID chargés en mémoire (réessaie au plus une fois par seconde).</summary>
    public static void Resolve()
    {
        bool missing = Traversal.Exists(m => m.Id == null || m.Item == null);
        if (!missing || Time.unscaledTime < nextResolve) return;
        nextResolve = Time.unscaledTime + 1f;
        try
        {
            foreach (var o in Resources.FindObjectsOfTypeAll(Il2CppType.Of<VaniaMoveID>()))
            {
                var id = o.TryCast<VaniaMoveID>();
                if (id == null) continue;
                string name = id.name.Replace("_VaniaMoveID", "");
                foreach (var m in Traversal)
                    if (m.Id == null && string.Equals(m.Key, name, StringComparison.OrdinalIgnoreCase)) m.Id = id;
            }
            foreach (var o in Resources.FindObjectsOfTypeAll(Il2CppType.Of<VaniaMoveItemData>()))
            {
                var item = o.TryCast<VaniaMoveItemData>();
                if (item == null) continue;
                string name = item.name.Replace("_ItemData", "");
                foreach (var m in Traversal)
                    if (m.Item == null && string.Equals(m.Key, name, StringComparison.OrdinalIgnoreCase)) m.Item = item;
            }
        }
        catch (Exception ex) { Plugin.Logger.LogWarning("[Compétences] " + ex.Message); }
    }

    static VaniaMovesComponent Component()
    {
        if (!MainCharacterSingleton.TryGetEntity(out Entity e) || e == null) return null;
        return e.GetComponent<VaniaMovesComponent>();
    }

    /// <summary>null = inconnu (pas en jeu ou asset non chargé).</summary>
    public static bool? IsUnlocked(Move m)
    {
        if (m.Id == null) return null;
        try
        {
            var c = Component();
            return c == null ? null : c.IsVaniaMoveUnlocked(m.Id);
        }
        catch { return null; }
    }

    public static bool Toggle(Move m, out string message)
    {
        message = null;
        var state = IsUnlocked(m);
        if (state == null) { message = "Ability unavailable (load a game first)"; return false; }
        try
        {
            var c = Component();
            bool ok = state.Value ? c.LockVaniaMove(m.Id) : c.UnlockVaniaMove(m.Id);
            bool now = c.IsVaniaMoveUnlocked(m.Id);
            message = $"{m.DisplayName}: {(now ? "enabled" : "disabled")}";
            Plugin.Logger.LogInfo($"[Compétences] {m.Key} → {(now ? "activée" : "désactivée")} (retour du jeu : {ok})");
            return true;
        }
        catch (Exception ex) { message = "Toggle failed: " + ex.Message; return false; }
    }
}
