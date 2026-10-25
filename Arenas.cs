using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Il2CppInterop.Runtime;
using UnityEngine;

namespace BelmontPractice;

/// <summary>
/// Arènes de boss, une par rencontre (certains boss se combattent deux fois, dans deux niveaux différents) :
/// clé « CharacterID_X@salle de l'arène ». Quand le joueur passe d'une salle normale à l'arène, sa dernière position
/// dans la salle d'avant (juste devant la porte, du bon côté) est retenue dans arenes.json. Arène inaccessible par sa
/// porte (fermée par l'histoire) : à défaut, la position du joueur dans l'arène (arrivé par téléportation / noclip).
/// Les boutons Fight et TP de l'onglet Bosses y ramènent le joueur.
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
        public string Boss { get; set; } = "";
        public string ArenaRoom { get; set; } = "";
        public string Area { get; set; } = "";
        /// <summary>Position dans l'arène elle-même (porte inconnue).</summary>
        public bool Inside { get; set; }
    }

    static Dictionary<string, Entry> entries;
    static string FilePath => Path.Combine(SlotStore.Folder, "..", "arenes.json");

    /// <summary>Change à chaque arène apprise (l'onglet Bosses se reconstruit).</summary>
    public static int Version { get; private set; }

    static string lastRoom;
    static bool lastRoomIsArena, insideTried;
    static BossData lastArenaData;
    static string lastArenaArcana;
    static float lastRoomSince, lastLoad = -100f, dumpAt;
    static readonly Queue<(float t, Vector3 p)> trail = new();

    // Demande de téléportation venue de l'interface, traitée par PracticeBehaviour
    static (LevelID level, Vector3 pos, bool fullReload, Bosses.Boss boss, string key)? request;

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
        // Ancien format : une entrée par boss, clé = CharacterID
        foreach (var (k, e) in entries) if (string.IsNullOrEmpty(e.Boss)) e.Boss = k.Split('@')[0];
        return entries;
    }

    static void SaveFile()
    {
        try { File.WriteAllText(FilePath, JsonSerializer.Serialize(entries, new JsonSerializerOptions { WriteIndented = true })); }
        catch (Exception ex) { Plugin.Logger.LogWarning("[Arènes] écriture : " + ex.Message); }
    }

    public static string KeyOf(Bosses.Boss b, string arenaRoom) => b.Id.name + "@" + arenaRoom;

    public static Entry Get(string key) => key != null && All().TryGetValue(key, out var e) ? e : null;

    public static int LevelNumber(string level) => int.TryParse(Regex.Match(level ?? "", @"LevelID_(\d+)").Groups[1].Value, out int n) ? n : 99;

    /// <summary>Rencontres connues d'un boss, dans l'ordre du jeu (numéro de niveau).</summary>
    public static List<(string key, Entry entry)> Encounters(Bosses.Boss b)
    {
        if (b?.Id == null) return new List<(string, Entry)>();
        return All().Where(kv => kv.Value.Boss == b.Id.name)
            .OrderBy(kv => LevelNumber(kv.Value.Level)).ThenBy(kv => kv.Value.Date)
            .Select(kv => (kv.Key, kv.Value)).ToList();
    }

    // ---------- Nom de zone affiché ----------

    static readonly Dictionary<string, string> areaCache = new();

    static LevelID FindLevel(string name)
    {
        foreach (var o in Resources.FindObjectsOfTypeAll(Il2CppType.Of<LevelID>()))
        {
            var l = o.TryCast<LevelID>();
            if (l != null && l.name == name) return l;
        }
        return null;
    }

    /// <summary>Nom du niveau dans la langue du jeu (Main Hall, Clock Tower…), sinon tiré de son identifiant.</summary>
    public static string AreaLabel(string levelName)
    {
        if (string.IsNullOrEmpty(levelName)) return "";
        if (areaCache.TryGetValue(levelName, out var cached)) return cached;
        string label = null;
        try
        {
            var ui = FindLevel(levelName)?.LoadLevelUIDisplayData();
            if (ui != null) label = Loc.Get(ui.levelDisplayName);
        }
        catch { }
        if (string.IsNullOrEmpty(label))
            label = Regex.Replace(Regex.Replace(levelName, "^LevelID_\\d*_?", ""), "(?<=[a-z])(?=[A-Z])", " ").Replace("_", " ");
        return areaCache[levelName] = label;
    }

    // ---------- Suivi du joueur ----------

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
            var data = GameBridge.CurrentRoomBoss(out string arcana);
            bool isArena = data != null || arcana != null;
            if (lastRoomIsArena) BossRespawn.OnLeaveArena();
            // Seulement en marchant : salle d'avant occupée au moins 1 s, et pas juste après un chargement
            bool walkedIn = lastRoom != null && !lastRoomIsArena && now - lastRoomSince >= 1f && now - lastLoad >= 3f;
            if (isArena && walkedIn) Learn(data, arcana, room, lastRoom, BeforeDoor(now), false);
            if (isArena)
            {
                if (Plugin.BossDiagnostics.Value) dumpAt = now + 2f; // la salle finit de charger ses objets après le passage de la porte
                var boss = BossOf(data, arcana);
                BossRespawn.OnEnterArena(boss, boss?.Id != null ? KeyOf(boss, room) : null);
            }
            lastRoom = room;
            lastRoomSince = now;
            lastRoomIsArena = isArena;
            lastArenaData = data;
            lastArenaArcana = arcana;
            insideTried = false;
            trail.Clear();
        }
        // Arène jamais entrée par sa porte (fermée par l'histoire…) : position dans l'arène après 2 s, faute de mieux
        if (lastRoomIsArena && !insideTried && now - lastRoomSince >= 2f && now - lastLoad >= 2f)
        {
            insideTried = true;
            var boss = BossOf(lastArenaData, lastArenaArcana);
            if (boss?.Id != null && Get(KeyOf(boss, room)) == null) Learn(lastArenaData, lastArenaArcana, room, room, pos, true);
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
            if ((data != null && b.DataName == data.name) || (arcana != null && b.Id?.name == arcana)) return b;
        return null;
    }

    static void Learn(BossData data, string arcana, string arenaRoom, string fromRoom, Vector3? pos, bool inside)
    {
        if (pos == null) return;
        var boss = BossOf(data, arcana);
        if (boss?.Id == null) return;
        string key = KeyOf(boss, arenaRoom);
        string level = GameBridge.CurrentLevelName();
        // La salle doit appartenir au niveau (« World_Biome_01_… » ↔ « LevelID_01_… »)
        string roomNum = Regex.Match(fromRoom ?? "", @"Biome_(\d+)").Groups[1].Value, levelNum = Regex.Match(level ?? "", @"LevelID_(\d+)").Groups[1].Value;
        if (roomNum.Length > 0 && levelNum.Length > 0 && roomNum != levelNum)
        {
            Plugin.Logger.LogInfo($"[Arènes] entrée ignorée : salle '{fromRoom}' hors du niveau {level}");
            return;
        }
        var old = Get(key);
        // Une porte par arène : la première franchie (une autre entrée peut être normalement verrouillée) ;
        // une porte remplace toujours une position prise dans l'arène.
        if (old != null && !old.Inside && (inside || old.Room != fromRoom))
        {
            if (!inside) Plugin.Logger.LogInfo($"[Arènes] {boss.Name} : autre entrée par '{fromRoom}', on garde '{old.Room}'");
            return;
        }
        // Ancien format (une entrée par boss) passant par la même porte : remplacé par cette rencontre
        if (!inside && All().TryGetValue(boss.Id.name, out var legacy) && legacy.Room == fromRoom) All().Remove(boss.Id.name);
        All()[key] = new Entry
        {
            Level = level ?? "", Room = fromRoom,
            X = pos.Value.x, Y = pos.Value.y, Z = pos.Value.z,
            Date = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            Boss = boss.Id.name, ArenaRoom = arenaRoom, Area = AreaLabel(level), Inside = inside,
        };
        SaveFile();
        Version++;
        Plugin.Logger.LogInfo($"[Arènes] {(inside ? "position dans l'arène" : "entrée de l'arène")} de {boss.Name} ({AreaLabel(level)}) retenue : {pos.Value} dans '{fromRoom}'");
    }

    /// <summary>Oublie l'entrée d'une arène (mauvaise porte : sortie fermée tant que le boss vit…) ; la prochaine est retenue.</summary>
    public static string Forget(Bosses.Boss b, string key)
    {
        if (key == null || !All().Remove(key)) return $"{b.Name}: no arena entrance to forget";
        SaveFile();
        Version++;
        Plugin.Logger.LogInfo($"[Arènes] entrée oubliée : {key}");
        return $"{b.Name}: arena entrance forgotten — walk into the arena through its usual door to set it again";
    }

    // ---------- Téléportation ----------

    /// <summary>Demande une téléportation devant l'arène d'une rencontre. Retourne le message à afficher.</summary>
    public static string RequestTeleport(Bosses.Boss b, string key)
    {
        var e = Get(key);
        if (e == null) return $"{b.Name}: enter its arena once so the mod can remember the door";
        var level = FindLevel(e.Level);
        if (level == null) return $"{b.Name}: level '{e.Level}' not found";
        // Boss remis « Alive » : ses conditions de combat sont imposées avant le TP et le niveau est rechargé en entier,
        // pour que l'arène se construise avec le boss (elle ne le prépare qu'à son chargement)
        bool full = b.Id != null && BossRespawn.Pending.Contains(b.Id.name);
        request = (level, new Vector3(e.X, e.Y, e.Z), full, b, key);
        string where = string.IsNullOrEmpty(e.Area) ? $"{b.Name}'s arena" : $"{b.Name}'s arena ({e.Area})";
        return full ? $"Teleport: in front of {where}, fight reset" : $"Teleport: in front of {where}";
    }

    /// <summary>
    /// Rechargement complet du niveau en restant à la position actuelle (dans l'arène), conditions du combat imposées :
    /// l'arène se reconstruit avec le boss.
    /// </summary>
    public static void RequestReloadHere(Bosses.Boss b, string key)
    {
        if (b?.Id == null || !BossRespawn.Pending.Contains(b.Id.name)) return;
        request = (null, GameBridge.PlayerPosition(), true, b, key); // niveau null = niveau actuel
    }

    public static bool TakeRequest(out LevelID level, out Vector3 pos, out bool fullReload, out Bosses.Boss boss, out string key)
    {
        level = null; pos = default; fullReload = false; boss = null; key = null;
        if (request == null) return false;
        (level, pos, fullReload, boss, key) = request.Value;
        request = null;
        return true;
    }
}
