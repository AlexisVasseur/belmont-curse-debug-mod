using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using MotherBase.EntityFramework;
using UnityEngine;

namespace BelmontPractice;

/// <summary>Reliques (CharmItemData) : possédées ou non, et 3 emplacements d'équipement.</summary>
public static class Relics
{
    public class Relic
    {
        public CharmItemData Item;
        public string Name => Loc.Get(Item.itemDisplayName, Item.name);
        public string Description => Loc.Get(Item.itemMainDescription);
    }

    public static readonly List<Relic> All = new();
    static float nextResolve;

    public static void Resolve()
    {
        if (All.Count > 0 || Time.unscaledTime < nextResolve || !ItemLibrary.Done) return;
        nextResolve = Time.unscaledTime + 2f;
        try
        {
            var seen = new HashSet<string>();
            foreach (var o in Resources.FindObjectsOfTypeAll(Il2CppType.Of<CharmItemData>()))
            {
                var c = o.TryCast<CharmItemData>();
                if (c == null || c.abstractItem || c.isDeprecated || string.IsNullOrEmpty(c.itemID) || !seen.Add(c.itemID)) continue;
                if (c.name.IndexOf("Doppelganger", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                All.Add(new Relic { Item = c });
            }
            All.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            if (All.Count > 0)
                Plugin.Logger.LogInfo($"[Reliques] {All.Count} reliques : {string.Join(", ", All.ConvertAll(r => r.Item.name))}");
        }
        catch (Exception ex) { Plugin.Logger.LogWarning("[Reliques] " + ex.Message); All.Clear(); }
    }

    static Inventory Inventory() =>
        MainCharacterSingleton.TryGetEntity(out Entity e) && e != null ? e.GetComponent<InventoryComponent>()?.inventory : null;

    public static bool? IsOwned(Relic r)
    {
        try
        {
            var inv = Inventory();
            if (inv == null) return null;
            foreach (var it in inv._itemsInInventory)
                if (it != null && it.itemID == r.Item.itemID) return true;
            return false;
        }
        catch { return null; }
    }

    public static bool IsEquipped(Relic r)
    {
        try { var inv = Inventory(); return inv != null && inv.HasCharmEquipped(r.Item); }
        catch { return false; }
    }

    public static int SlotsCount()
    {
        try { return Inventory()?.GetCharmsSlotsCount() ?? 3; } catch { return 3; }
    }

    public static int EquippedCount()
    {
        try { return Inventory()?.GetEquippedCharmsCount() ?? 0; } catch { return 0; }
    }

    /// <summary>Clic : ajoute la relique à l'inventaire, ou la retire (en la déséquipant d'abord).</summary>
    public static string Toggle(Relic r)
    {
        var inv = Inventory();
        var owned = IsOwned(r);
        if (inv == null || owned == null) return "Relic unavailable (load a game first)";
        try
        {
            if (owned.Value)
            {
                if (IsEquipped(r)) inv.UnequipCharm(r.Item);
                inv.RemoveItemFromInventory(r.Item.itemID);
            }
            else
            {
                inv.AddItemToInventory(r.Item, false);
                if (IsOwned(r) != true) inv.AddItemToInventoryFromInit(r.Item, false);
            }
            bool now = IsOwned(r) == true;
            Plugin.Logger.LogInfo($"[Reliques] {r.Item.name} → {(now ? "ajoutée" : "retirée")}");
            return $"{r.Name}: {(now ? "unlocked" : "removed")}";
        }
        catch (Exception ex) { return "Toggle failed: " + ex.Message; }
    }

    /// <summary>Clic droit : équipe ou déséquipe la relique (dans la limite des emplacements).</summary>
    public static string ToggleEquip(Relic r)
    {
        var inv = Inventory();
        if (inv == null) return "Relic unavailable (load a game first)";
        try
        {
            if (IsEquipped(r))
            {
                inv.UnequipCharm(r.Item);
                return $"{r.Name}: unequipped";
            }
            if (!inv.HasAvailableCharmSlot()) return $"All {SlotsCount()} relic slots are used: unequip one first (right-click)";
            if (IsOwned(r) != true) inv.AddItemToInventory(r.Item, false);
            inv.EquipCharmFromInventory(r.Item, -1);
            Plugin.Logger.LogInfo($"[Reliques] {r.Item.name} équipée");
            return IsEquipped(r) ? $"{r.Name}: equipped" : $"{r.Name}: can't be equipped";
        }
        catch (Exception ex) { return "Equip failed: " + ex.Message; }
    }
}
