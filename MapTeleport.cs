using System;
using System.Text;
using Il2CppInterop.Runtime;
using MotherBase.EntityFramework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BelmontPractice;

/// <summary>
/// Téléportation depuis la carte du niveau (première version, à valider en jeu) :
/// carte ouverte, la touche Map teleport (T par défaut) ou le clic molette mémorise le point sous le curseur ;
/// à la fermeture de la carte, le joueur y est téléporté (même mécanisme qu'une savestate, derrière le noir).
/// La position carte → monde est déduite de la position du joueur sur la carte (décalage, échelle 1 supposée) ;
/// tout est noté dans le journal pour vérifier la correspondance et lister les miroirs du niveau.
/// </summary>
public static class MapTeleport
{
    static LevelMapContext context;
    static float nextFind;
    static bool mapWasOpen;
    static Vector3? pending;
    static LevelID pendingLevel;
    /// <summary>Niveau de la destination choisie (celui affiché sur la carte).</summary>
    public static LevelID PendingLevel { get; private set; }
    static bool mirrorsLogged;

    /// <summary>Carte du niveau du joueur ouverte à l'écran.</summary>
    public static bool MapOpen
    {
        get
        {
            try { return context != null && context.gameObject.activeInHierarchy; } // n'importe quel niveau affiché
            catch { context = null; return false; }
        }
    }

    /// <summary>Chaque frame, en jeu. Retourne une destination à charger quand la carte vient de se fermer.</summary>
    public static Vector3? Update(Keyboard kb)
    {
        try
        {
            if (context == null && Time.unscaledTime >= nextFind)
            {
                nextFind = Time.unscaledTime + 1f;
                context = UnityEngine.Object.FindObjectOfType(Il2CppType.Of<LevelMapContext>())?.TryCast<LevelMapContext>();
            }
            bool open = MapOpen;
            if (open)
            {
                if (!mirrorsLogged) { mirrorsLogged = true; LogMirrors(); }
                bool key = kb != null && Keybinds.MapTeleport.Pressed(kb);
                bool middle = Mouse.current != null && Mouse.current.middleButton.wasPressedThisFrame;
                if (key || middle) Pick();
            }
            if (mapWasOpen && !open && pending != null)
            {
                var target = pending;
                PendingLevel = pendingLevel;
                pending = null;
                mapWasOpen = false;
                return target;
            }
            mapWasOpen = open;
            if (!open) mirrorsLogged = false;
        }
        catch (Exception ex) { Plugin.Logger.LogWarning("[Carte] " + ex.Message); }
        return null;
    }

    static void Pick()
    {
        if (!MainCharacterSingleton.TryGetEntity(out Entity e) || e == null) return;
        // La carte est à l'échelle du niveau (vérifié : position du joueur sur la carte = position dans le monde)
        Vector2 cursor = context.currentCursorPosition;
        var target = new Vector3(cursor.x, cursor.y, e.transform.position.z);
        string what = "map point";
        // Curseur sur un miroir : destination = le miroir lui-même
        var mirror = context.currentItemSelection?.TryCast<LevelMapMirrorItem>();
        if (mirror != null)
        {
            var mp = mirror.transform.position;
            target = new Vector3(mp.x, mp.y, target.z);
            what = "mirror";
        }
        pending = target;
        pendingLevel = context.level;
        string lvl = context.level != null ? context.level.name : "?";
        Plugin.Logger.LogInfo($"[Carte] {what} choisi : curseur={cursor} niveau carte={lvl} (niveau du joueur : {(context.isCurrentMcLevel ? "oui" : "non")}) → destination {target}");
        Notify?.Invoke($"Teleport target set ({what}, {lvl}): close the map to go there", false);
    }

    static void LogMirrors()
    {
        try
        {
            var sb = new StringBuilder("[Carte] miroirs connus :");
            int n = 0;
            foreach (var o in Resources.FindObjectsOfTypeAll(Il2CppType.Of<LevelMapMirrorItem>()))
            {
                var m = o.TryCast<LevelMapMirrorItem>();
                if (m == null) continue;
                n++;
                sb.Append($"\n  {m.name} salle={m.itemWorldRoomId} carte={m.transform.position} actif={m.gameObject.activeInHierarchy}");
            }
            sb.Append($"\n  ({n} miroir(s) ; joueur sur la carte = {context.GetCurrentMcLevelMcMapPosition(false)})");
            Plugin.Logger.LogInfo(sb.ToString());
        }
        catch (Exception ex) { Plugin.Logger.LogWarning("[Carte] miroirs : " + ex.Message); }
    }

    public static Action<string, bool> Notify;
}
