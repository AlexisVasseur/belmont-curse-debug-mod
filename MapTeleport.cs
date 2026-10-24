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
    static bool mirrorsLogged;

    /// <summary>Carte du niveau du joueur ouverte à l'écran.</summary>
    public static bool MapOpen
    {
        get
        {
            try { return context != null && context.gameObject.activeInHierarchy && context.isCurrentMcLevel; }
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
        Vector2 cursor = context.currentCursorPosition;
        Vector3 mcMap = context.GetCurrentMcLevelMcMapPosition(false);
        Vector3 player = e.transform.position;
        // Hypothèse : la carte est à l'échelle du monde, à un décalage près (validé par le journal)
        var target = new Vector3(player.x + (cursor.x - mcMap.x), player.y + (cursor.y - mcMap.y), player.z);
        pending = target;
        Plugin.Logger.LogInfo($"[Carte] point choisi : curseur carte={cursor} joueur carte={mcMap} joueur monde={player} → destination {target}");
        Notify?.Invoke($"Teleport target set ({target.x:0}, {target.y:0}): close the map to go there", false);
    }

    static void LogMirrors()
    {
        try
        {
            var sb = new StringBuilder("[Carte] miroirs du niveau :");
            int n = 0;
            foreach (var it in context.staticLevelMapItems)
            {
                var m = it?.TryCast<LevelMapMirrorItem>();
                if (m == null) continue;
                n++;
                sb.Append($"\n  {m.name} salle={m.itemWorldRoomId} carte={m.transform.position} direction={m.direction}");
            }
            sb.Append($"\n  ({n} miroir(s) ; joueur sur la carte = {context.GetCurrentMcLevelMcMapPosition(false)})");
            Plugin.Logger.LogInfo(sb.ToString());
        }
        catch (Exception ex) { Plugin.Logger.LogWarning("[Carte] miroirs : " + ex.Message); }
    }

    public static Action<string, bool> Notify;
}
