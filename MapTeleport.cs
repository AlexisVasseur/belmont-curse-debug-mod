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
        // Autre niveau : seulement un niveau déjà visité, jamais une fin (charger « 12_MQBadEnding » faisait planter le jeu)
        if (!context.isCurrentMcLevel && !CanTravelTo(context.level, out string reason))
        {
            Plugin.Logger.LogInfo($"[Carte] téléportation refusée vers {(context.level != null ? context.level.name : "?")} : {reason}");
            Notify?.Invoke("Can't teleport there: " + reason, true);
            return;
        }

        // La carte est à l'échelle du niveau (vérifié : position du joueur sur la carte = position dans le monde)
        Vector2 cursor = context.currentCursorPosition;
        var target = new Vector3(cursor.x, cursor.y, e.transform.position.z);
        string what = "map point";
        // Près d'un miroir (Mirror of Life = point de sauvegarde, miroir de voyage rapide) ou d'une transition
        // vers un autre quartier : on se pose exactement dessus (viser à côté envoyait hors des salles)
        var snap = NearestDestination(cursor, out string kind);
        if (snap != null)
        {
            var sp = snap.transform.position;
            target = new Vector3(sp.x, sp.y, target.z);
            what = kind;
            Plugin.Logger.LogInfo($"[Carte] {kind} le plus proche : {snap.name} salle={snap.itemWorldRoomId} position={sp}");
        }
        pending = target;
        pendingLevel = context.level;
        string lvl = context.level != null ? context.level.name : "?";
        Plugin.Logger.LogInfo($"[Carte] {what} choisi : curseur={cursor} niveau carte={lvl} (niveau du joueur : {(context.isCurrentMcLevel ? "oui" : "non")}) → destination {target}");
        Notify?.Invoke($"Teleport target set ({what}, {lvl}): close the map to go there", false);
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
