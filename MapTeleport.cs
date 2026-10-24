using System;
using System.Text;
using Il2CppInterop.Runtime;
using MotherBase.EntityFramework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BelmontPractice;

/// <summary>
/// Téléportation depuis la carte (la carte est à l'échelle du niveau) :
/// 1er appui (Map teleport = T, clic molette ou Start à la manette) : fenêtre « Teleport to … ? » ;
/// 2e appui : la carte se ferme et le joueur est téléporté (chargement de savestate, derrière le noir).
/// Bouger le curseur, Échap / B ou fermer la carte annule. Près d'un miroir ou d'un passage, on s'y accroche.
/// Un encadré rappelle les commandes tant que la carte est ouverte.
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

    // Fenêtre de confirmation en cours
    static bool confirming;
    static Vector3 askTarget;
    static LevelID askLevel;
    static Vector2 askCursor;

    /// <summary>Carte ouverte à l'écran (n'importe quel niveau affiché).</summary>
    public static bool MapOpen
    {
        get
        {
            try { return context != null && context.gameObject.activeInHierarchy; }
            catch { context = null; return false; }
        }
    }

    static bool TeleportPressed(Keyboard kb) =>
        (kb != null && Keybinds.MapTeleport.Pressed(kb))
        || (Mouse.current != null && Mouse.current.middleButton.wasPressedThisFrame)
        || (Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame);

    static bool CancelPressed(Keyboard kb) =>
        (kb != null && kb.escapeKey.wasPressedThisFrame)
        || (Gamepad.current != null && Gamepad.current.buttonEast.wasPressedThisFrame);

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
                if (confirming)
                {
                    if (TeleportPressed(kb)) Confirm();
                    else if (CancelPressed(kb) || Vector2.Distance(context.currentCursorPosition, askCursor) > 1.5f) Cancel();
                }
                else if (TeleportPressed(kb)) Ask();
            }
            else if (confirming) Cancel();

            MapTeleportView.Update(open, confirming, open ? CursorCanvasPosition() : null);

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

    /// <summary>1er appui : calcule la destination et ouvre la fenêtre de confirmation.</summary>
    static void Ask()
    {
        if (!MainCharacterSingleton.TryGetEntity(out Entity e) || e == null) return;
        string area = AreaName(context.level);
        // Autre niveau : seulement un niveau déjà visité, jamais une fin (charger « 12_MQBadEnding » faisait planter le jeu)
        if (!context.isCurrentMcLevel && !CanTravelTo(context.level, out string reason))
        {
            Plugin.Logger.LogInfo($"[Carte] téléportation refusée vers {(context.level != null ? context.level.name : "?")} : {reason}");
            Notify?.Invoke("Can't teleport there: " + reason, true);
            return;
        }

        Vector2 cursor = context.currentCursorPosition;
        var target = new Vector3(cursor.x, cursor.y, e.transform.position.z);
        string what = "this point";
        // Près d'un miroir (Mirror of Life = point de sauvegarde, miroir de voyage rapide) ou d'un passage :
        // on se pose exactement dessus (viser à côté envoyait hors des salles)
        var snap = NearestDestination(cursor, out string kind);
        if (snap != null)
        {
            var sp = snap.transform.position;
            target = new Vector3(sp.x, sp.y, target.z);
            what = "the " + kind;
            Plugin.Logger.LogInfo($"[Carte] {kind} le plus proche : {snap.name} salle={snap.itemWorldRoomId} position={sp}");
        }
        confirming = true;
        askTarget = target;
        askLevel = context.level;
        askCursor = cursor;
        MapTeleportView.SetQuestion($"Teleport to {what}{(context.isCurrentMcLevel ? "" : " in " + area)}?");
        Plugin.Logger.LogInfo($"[Carte] confirmation demandée : {what} niveau={(context.level != null ? context.level.name : "?")} destination={target}");
    }

    /// <summary>2e appui : ferme la carte ; la téléportation part à sa fermeture.</summary>
    static void Confirm()
    {
        confirming = false;
        pending = askTarget;
        pendingLevel = askLevel;
        try
        {
            var menu = UnityEngine.Object.FindObjectOfType(Il2CppType.Of<GameMenuManager>())?.TryCast<GameMenuManager>();
            if (menu != null) menu.Close();
            else Notify?.Invoke("Close the map to teleport", false);
        }
        catch (Exception ex) { Plugin.Logger.LogWarning("[Carte] fermeture du menu : " + ex.Message); Notify?.Invoke("Close the map to teleport", false); }
    }

    static void Cancel() => confirming = false;

    static LevelMapCamera mapCam;
    static UnityEngine.UI.RawImage mapImage;
    static bool placementLogged;

    /// <summary>
    /// Position du viseur de la carte dans le canvas du mod (origine en bas à gauche, unités de référence).
    /// La carte a sa propre caméra : soit elle dessine à l'écran, soit dans une texture affichée par une RawImage.
    /// </summary>
    static Vector2? CursorCanvasPosition()
    {
        try
        {
            if (mapCam == null) mapCam = UnityEngine.Object.FindObjectOfType(Il2CppType.Of<LevelMapCamera>())?.TryCast<LevelMapCamera>();
            var cam = mapCam?.mapCamera;
            if (cam == null) return null;
            Vector2 c = context.currentCursorPosition;
            var world = new Vector3(c.x, c.y, mapCam.cursor != null ? mapCam.cursor.transform.position.z : 0f);
            Vector2 screen;
            var rt = cam.targetTexture;
            if (rt == null)
            {
                Vector3 s = cam.WorldToScreenPoint(world);
                screen = new Vector2(s.x, s.y);
                if (!placementLogged) { placementLogged = true; Plugin.Logger.LogInfo($"[Carte] viseur : caméra de carte à l'écran, {screen}"); }
            }
            else
            {
                if (mapImage == null || mapImage.texture != rt)
                {
                    mapImage = null;
                    foreach (var o in UnityEngine.Object.FindObjectsOfType(Il2CppType.Of<UnityEngine.UI.RawImage>()))
                    {
                        var ri = o.TryCast<UnityEngine.UI.RawImage>();
                        if (ri != null && ri.texture != null && ri.texture.Pointer == rt.Pointer) { mapImage = ri; break; }
                    }
                }
                if (mapImage == null) return null;
                Vector3 vp = cam.WorldToViewportPoint(world);
                var corners = new Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppStructArray<Vector3>(4);
                mapImage.rectTransform.GetWorldCorners(corners);
                var canvas = mapImage.canvas;
                Camera uiCam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
                Vector3 a = corners[0], b = corners[2];
                if (uiCam != null) { a = uiCam.WorldToScreenPoint(a); b = uiCam.WorldToScreenPoint(b); }
                screen = new Vector2(Mathf.Lerp(a.x, b.x, vp.x), Mathf.Lerp(a.y, b.y, vp.y));
                if (!placementLogged) { placementLogged = true; Plugin.Logger.LogInfo($"[Carte] viseur : texture de carte affichée par {mapImage.name}, {screen}"); }
            }
            float scale = UiCanvas.Canvas != null && UiCanvas.Canvas.scaleFactor > 0f ? UiCanvas.Canvas.scaleFactor : 1f;
            return screen / scale;
        }
        catch (Exception ex) { Plugin.Logger.LogWarning("[Carte] position du viseur : " + ex.Message); return null; }
    }

    /// <summary>« LevelID_03_Catacombs » → « Catacombs ».</summary>
    static string AreaName(LevelID level)
    {
        if (level == null) return "?";
        string n = System.Text.RegularExpressions.Regex.Replace(level.name, "^LevelID_\\d*_?", "");
        return System.Text.RegularExpressions.Regex.Replace(n, "(?<=[a-z])(?=[A-Z])", " ").Replace("_", " ");
    }
    const float SnapRadius = 12f;

    static bool CanTravelTo(LevelID level, out string reason)
    {
        reason = null;
        if (level == null) { reason = "unknown area"; return false; }
        if (level.name.IndexOf("Ending", StringComparison.OrdinalIgnoreCase) >= 0) { reason = "story ending area"; return false; }
        try
        {
            var entered = SaveLogic.Get()?._currentSave?.playerSave?.enteredLevels;
            if (entered != null)
            {
                for (int i = 0; i < entered.Length; i++) if (entered[i] == level.guid) return true;
                reason = "area never visited";
                return false;
            }
        }
        catch (Exception ex) { Plugin.Logger.LogWarning("[Carte] niveaux visités : " + ex.Message); }
        return true;
    }

    /// <summary>Miroir ou transition de la carte le plus proche du curseur (dans SnapRadius), sinon null.</summary>
    static LevelMapItem NearestDestination(Vector2 cursor, out string kind)
    {
        string bestKind = null;
        LevelMapItem best = null;
        float bestD = SnapRadius;
        void Scan(Il2CppSystem.Collections.Generic.List<LevelMapItem> list)
        {
            if (list == null) return;
            for (int i = 0; i < list.Count; i++)
            {
                var it = list[i];
                if (it == null || !it.gameObject.activeInHierarchy) continue;
                string k = KindOf(it);
                if (k == null) continue;
                var p = it.transform.position;
                float d = Vector2.Distance(cursor, new Vector2(p.x, p.y));
                if (d < bestD) { bestD = d; best = it; bestKind = k; }
            }
        }
        Scan(context.staticLevelMapItems);
        Scan(context.dynamicLevelMapItems);
        kind = bestKind;
        return best;
    }

    /// <summary>Type de destination « accrochable » : miroir de sauvegarde, miroir de voyage rapide, transition.</summary>
    static string KindOf(LevelMapItem it)
    {
        if (it.TryCast<LevelMapSavePointItem>() != null) return "Mirror of Life";
        string n = it.name ?? "";
        if (n.Contains("FastTravel")) return "fast travel mirror";
        if (it.TryCast<LevelMapBiomeTransitionItem>() != null) return "district passage";
        return null;
    }

    /// <summary>Recensement des éléments de la carte (par type) pour identifier les miroirs ; une fois par ouverture.</summary>
    static void LogMirrors()
    {
        try
        {
            var counts = new System.Collections.Generic.Dictionary<string, int>();
            var detail = new StringBuilder();
            void Scan(Il2CppSystem.Collections.Generic.List<LevelMapItem> list, string origin)
            {
                if (list == null) return;
                for (int i = 0; i < list.Count; i++)
                {
                    var it = list[i];
                    if (it == null) continue;
                    string type = it.GetIl2CppType().Name;
                    counts[type] = counts.TryGetValue(type, out int n) ? n + 1 : 1;
                    string nm = it.name ?? "";
                    if (type.Contains("Mirror") || type.Contains("FastTravel") || nm.Contains("Mirror") || nm.Contains("FastTravel") || nm.Contains("Save"))
                        detail.Append($"\n  [{origin}] {type} '{nm}' salle={it.itemWorldRoomId} position={it.transform.position} actif={it.gameObject.activeInHierarchy}");
                }
            }
            Scan(context.staticLevelMapItems, "statique");
            Scan(context.dynamicLevelMapItems, "dynamique");
            var sb = new StringBuilder($"[Carte] éléments de la carte ({(context.level != null ? context.level.name : "?")}) :");
            foreach (var kv in counts) sb.Append($" {kv.Key}×{kv.Value}");
            sb.Append(detail);
            Plugin.Logger.LogInfo(sb.ToString());
        }
        catch (Exception ex) { Plugin.Logger.LogWarning("[Carte] recensement : " + ex.Message); }
    }
    public static Action<string, bool> Notify;
}
