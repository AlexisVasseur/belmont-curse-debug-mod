using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using BepInEx;

namespace BelmontPractice;

/// <summary>
/// Collection de savestates organisée comme DebugMod : des pages de 10 emplacements,
/// stockées dans Savestates/&lt;page&gt;/savestate&lt;i&gt;.json. Le quickslot est dans Savestates/quickslot.json.
/// </summary>
public static class SlotStore
{
    public const int SlotsPerPage = 10;

    public static readonly string Folder = Path.Combine(Paths.GameRootPath, "BelmontPractice", "Savestates");
    static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    static readonly List<SaveState[]> pages = new();

    public static int PageCount => pages.Count;

    static string PagePath(int page) => Path.Combine(Folder, page.ToString());
    static string SlotPath(int page, int index) => Path.Combine(PagePath(page), $"savestate{index}.json");
    static string QuickslotPath => Path.Combine(Folder, "quickslot.json");

    // ---------- Chargement ----------

    public static void LoadAll()
    {
        try
        {
            Directory.CreateDirectory(Folder);
            MigrateFlatLayout();
            pages.Clear();
            foreach (var dir in Directory.EnumerateDirectories(Folder))
            {
                var m = Regex.Match(Path.GetFileName(dir), "^(\\d+)$");
                if (!m.Success) continue;
                int page = int.Parse(m.Groups[1].Value);
                while (pages.Count <= page) pages.Add(new SaveState[SlotsPerPage]);
                foreach (var file in Directory.EnumerateFiles(dir))
                {
                    var f = Regex.Match(Path.GetFileName(file), "^savestate(\\d)\\.json$");
                    if (f.Success) pages[page][int.Parse(f.Groups[1].Value)] = ReadFile(file);
                }
            }
            if (pages.Count == 0) AddPage(0);
        }
        catch (Exception ex) { Plugin.Logger.LogError("[Savestates] chargement : " + ex); }
    }

    /// <summary>Anciennes versions du mod : savestate{N}.json à plat → page N/10, emplacement N%10.</summary>
    static void MigrateFlatLayout()
    {
        foreach (var file in Directory.EnumerateFiles(Folder, "savestate*.json"))
        {
            var m = Regex.Match(Path.GetFileName(file), "^savestate(\\d+)\\.json$");
            if (!m.Success) continue;
            int n = int.Parse(m.Groups[1].Value);
            string target = SlotPath(n / SlotsPerPage, n % SlotsPerPage);
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            if (!File.Exists(target)) File.Move(file, target);
        }
    }

    static SaveState ReadFile(string path)
    {
        try { return JsonSerializer.Deserialize<SaveState>(File.ReadAllText(path)); }
        catch (Exception ex) { Plugin.Logger.LogWarning($"Savestate illisible {path} : {ex.Message}"); return null; }
    }

    static void WriteSlot(int page, int index)
    {
        string path = SlotPath(page, index);
        var state = pages[page][index];
        if (state != null)
        {
            Directory.CreateDirectory(PagePath(page));
            File.WriteAllText(path, JsonSerializer.Serialize(state, JsonOptions));
        }
        else if (File.Exists(path)) File.Delete(path);
    }

    // ---------- Emplacements ----------

    public static SaveState Get(int page, int index) =>
        page >= 0 && page < pages.Count ? pages[page][index] : null;

    public static void Set(int page, int index, SaveState state)
    {
        if (state == null) return;
        pages[page][index] = state.Clone();
        WriteSlot(page, index);
    }

    public static void Delete(int page, int index)
    {
        pages[page][index] = null;
        WriteSlot(page, index);
    }

    public static bool Rename(int page, int index, string name)
    {
        var s = pages[page][index];
        if (s == null || string.IsNullOrWhiteSpace(name)) return false;
        s.Name = name.Trim();
        WriteSlot(page, index);
        return true;
    }

    public static void Swap(int p1, int i1, int p2, int i2)
    {
        (pages[p1][i1], pages[p2][i2]) = (pages[p2][i2], pages[p1][i1]);
        WriteSlot(p1, i1);
        WriteSlot(p2, i2);
    }

    // ---------- Pages ----------

    public static void AddPage(int at)
    {
        for (int p = pages.Count - 1; p >= at; p--)
            if (Directory.Exists(PagePath(p))) Directory.Move(PagePath(p), PagePath(p + 1));
        Directory.CreateDirectory(PagePath(at));
        pages.Insert(at, new SaveState[SlotsPerPage]);
    }

    public static bool PageHasStates(int page) => pages[page].Any(s => s != null);

    /// <summary>Supprime une page (et ses savestates). Il reste toujours au moins une page.</summary>
    public static void RemovePage(int page)
    {
        if (pages.Count <= 1)
        {
            for (int i = 0; i < SlotsPerPage; i++) Delete(page, i);
            return;
        }
        if (Directory.Exists(PagePath(page))) Directory.Delete(PagePath(page), true);
        pages.RemoveAt(page);
        for (int p = page; p < pages.Count; p++)
            if (Directory.Exists(PagePath(p + 1))) Directory.Move(PagePath(p + 1), PagePath(p));
    }

    public static void SwapPages(int a, int b)
    {
        if (a == b || a < 0 || b < 0 || a >= pages.Count || b >= pages.Count) return;
        string temp = Path.Combine(Folder, "_temp");
        Directory.CreateDirectory(PagePath(a));
        Directory.CreateDirectory(PagePath(b));
        Directory.Move(PagePath(a), temp);
        Directory.Move(PagePath(b), PagePath(a));
        Directory.Move(temp, PagePath(b));
        (pages[a], pages[b]) = (pages[b], pages[a]);
    }

    // ---------- Quickslot ----------

    public static SaveState LoadQuickslot() => File.Exists(QuickslotPath) ? ReadFile(QuickslotPath) : null;

    public static void SaveQuickslot(SaveState state)
    {
        try
        {
            Directory.CreateDirectory(Folder);
            if (state == null) { if (File.Exists(QuickslotPath)) File.Delete(QuickslotPath); }
            else File.WriteAllText(QuickslotPath, JsonSerializer.Serialize(state, JsonOptions));
        }
        catch (Exception ex) { Plugin.Logger.LogWarning("[Quickslot] " + ex.Message); }
    }
}
