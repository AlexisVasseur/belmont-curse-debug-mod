using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace BelmontPractice;

/// <summary>
/// Devant chaque arène : quand le joueur passe d'une salle normale à une salle de boss, sa dernière position
/// dans la salle d'avant (juste devant la porte, du bon côté) est retenue dans arenes.json.
/// Le bouton TP de l'onglet Bosses y ramène le joueur.
/// </summary>
public static class Arenas
{
    public class Entry
    {
        public string Level { get; set; } = "";
        public string Room { get; set; } = "";
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }
        public string Date { get; set; } = "";
    }

    static Dictionary<string, Entry> entries;
    static string FilePath => Path.Combine(SlotStore.Folder, "..", "arenes.json");

    static string lastRoom;
    static bool lastRoomIsArena;
    static float lastRoomSince, lastLoad = -100f, dumpAt;
    static readonly Queue<(float t, Vector3 p)> trail = new();

    // Demande de téléportation venue de l'interface, traitée par PracticeBehaviour
    static (LevelID level, Vector3 pos)? request;

    static Dictionary<string, Entry> All()
    {
        if (entries != null) return entries;
        entries = new Dictionary<string, Entry>();
        try
        {
            if (File.Exists(FilePath))
                entries = JsonSerializer.Deserialize<Dictionary<string, Entry>>(File.ReadAllText(FilePath)) ?? entries;
        }
        catch (Exception ex) { Plugin.Logger.LogWarning("[Arènes] lecture : " + ex.Message); }
        return entries;
    }

    static void SaveFile()
    {
        try { File.WriteAllText(FilePath, JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true })); }
        catch (Exception ex) { Plugin.Logger.LogWarning("[Arènes] écriture : " + ex.Message); }
    }

    public static Entry Get(Bosses.Boss b) => b?.Id != null && All().TryGetValue(b.Id.name, out var e) ? e : null;

    /// <summary>Chaque frame en jeu. loading : chargement du mod en cours (téléportation, savestate) → on n'apprend rien.</summary>
    public static void Update(bool loading)
    {
        float now = Time.unscaledTime;
        if (loading)
        {
            if (lastRoomIsArena) BossRespawn.OnLeaveArena();
            lastRoom = null; lastRoomIsArena = false; trail.Clear(); lastLoad = now;
            return;
        }
        string room = GameBridge.CurrentRoomId();
        if (string.IsNullOrEmpty(room)) return;
        Vector3 pos = GameBridge.PlayerPosition();

        if (room != lastRoom)
        {
            var boss = GameBridge.CurrentRoomBoss(out string arcana);
            bool isArena = boss != null || arcana != null;
            if (lastRoomIsArena) BossRespawn.OnLeaveArena();
            // Seulement en marchant : salle d'avant occupée au moins 1 s, et pas juste après un chargement
            bool walkedIn = lastRoom != null && !lastRoomIsArena && now - lastRoomSince >= 1f && now - lastLoad >= 3f;
            if (isArena && walkedIn) Learn(boss, arcana, lastRoom, BeforeDoor(now));
            if (isArena) { dumpAt = now + 2f; BossRespawn.OnEnterArena(BossOf(boss, arcana)); } // la salle finit de charger ses objets après le passage de la porte
            lastRoom = room;
            lastRoomSince = now;
            lastRoomIsArena = isArena;
            trail.Clear();
        }
        BossRespawn.Update();
        if (dumpAt > 0f && now >= dumpAt) { dumpAt = 0f; BossDiag.DumpDirectors("2 s après l'entrée dans l'arène"); }
        trail.Enqueue((now, pos));
        while (trail.Count > 0 && trail.Peek().t < now - 2f) trail.Dequeue();
    }

    /// <summary>Position environ 0,35 s avant le changement de salle : encore dans la salle d'avant, avant la porte.</summary>
    static Vector3? BeforeDoor(float now)
    {
        Vector3? best = null;
        foreach (var (t, p) in trail)
        {
            if (best == null || t <= now - 0.35f) best = p;
            else break;
        }
        return best;
    }

    static Bosses.Boss BossOf(BossData data, string arcana)
    {
        foreach (var b in Bosses.All)
            if ((data != null && b.Data != null && b.Data.name == data.name) || (arcana != null && b.Id?.name == arcana)) return b;
        return null;
    }

    static void Learn(BossData data, string arcana, string fromRoom, Vector3? pos)
    {
        if (pos == null) return;
        var boss = BossOf(data, arcana);
        string key = boss?.Id?.name ?? arcana ?? data?.name;
        if (key == null) return;
        string level = GameBridge.CurrentLevelName();
        // La salle doit appartenir au niveau (« World_Biome_01_… » ↔ « LevelID_01_… »)
        string roomNum = Regex.Match(fromRoom ?? "", @"Biome_(\d+)").Groups[1].Value, levelNum = Regex.Match(level ?? "", @"LevelID_(\d+)").Groups[1].Value;
        if (roomNum.Length > 0 && levelNum.Length > 0 && roomNum != levelNum)
        {
            Plugin.Logger.LogInfo($"[Arènes] entrée ignorée : salle '{fromRoom}' hors du niveau {level}");
            return;
        }
        // Une seule entrée par boss : la première porte franchie (une autre entrée peut être normalement verrouillée)
        if (All().TryGetValue(key, out var old) && old.Room != fromRoom)
        {
            Plugin.Logger.LogInfo($"[Arènes] {boss?.Name ?? key} : autre entrée par '{fromRoom}', on garde '{old.Room}'");
            return;
        }
        All()[key] = new Entry
        {
            Level = level ?? "", Room = fromRoom,
            X = pos.Value.x, Y = pos.Value.y, Z = pos.Value.z,
            Date = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
        };
        SaveFile();
        Plugin.Logger.LogInfo($"[Arènes] entrée de l'arène de {boss?.Name ?? key} retenue : {pos.Value} dans '{fromRoom}' ({level})");
    }

    /// <summary>Demande une téléportation devant l'arène. Retourne le message à afficher.</summary>
    public static string RequestTeleport(Bosses.Boss b)
    {
        var e = Get(b);
        if (e == null) return $"{b.Name}: enter its arena once so the mod can remember the door";
        LevelID level = null;
        foreach (var o in Resources.FindObjectsOfTypeAll(Il2CppType.Of<LevelID>()))
        {
            var l = o.TryCast<LevelID>();
            if (l != null && l.name == e.Level) { level = l; break; }
        }
        if (level == null) return $"{b.Name}: level '{e.Level}' not found";
        request = (level, new Vector3(e.X, e.Y, e.Z));
        return $"Teleport: in front of {b.Name}'s arena";
    }

    public static bool TakeRequest(out LevelID level, out Vector3 pos)
    {
        level = null; pos = default;
        if (request == null) return false;
        (level, pos) = request.Value;
        request = null;
        return true;
    }
}
