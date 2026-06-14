using System;
using System.Collections.Generic;
using ExileCore2;
using ExileCore2.PoEMemory.Components;
using ExileCore2.PoEMemory.Elements;
using ExileCore2.PoEMemory.Elements.InventoryElements;
using ExileCore2.PoEMemory.MemoryObjects;
using StashValueTracker.Model;
using StashValueTracker.Pricing;

namespace StashValueTracker.Scanning;

public sealed class StashScanner
{
    private readonly GameController _gc;
    private readonly NinjaPricerBridge _bridge;
    private readonly Action<string> _logError;

    public StashScanner(GameController gc, NinjaPricerBridge bridge, Action<string> logError)
    {
        _gc = gc;
        _bridge = bridge;
        _logError = logError;
    }

    /// <summary>The visible normal player stash, or null when no stash tab is open.</summary>
    public StashElement? GetVisibleStash()
    {
        var stash = _gc.IngameState.IngameUi.StashElement;
        return stash is { IsVisible: true } ? stash : null;
    }

    /// <summary>Number of items currently visible in the open tab (for change detection). 0 if none.</summary>
    public int CurrentItemCount(StashElement stash) =>
        stash?.VisibleStash?.VisibleInventoryItems?.Count ?? 0;

    /// <summary>
    /// Opaque, stable-as-possible identity for the visible tab. Prefers the tab name (survives
    /// reorder); falls back to the index. If a reliable per-tab id is confirmed in-game, prefer it here.
    /// </summary>
    public string ResolveTabKey(StashElement stash)
    {
        // StashElement.Inventories is List<StashTabContainerInventory>; each entry has .TabName.
        // AllStashNames and GetStashName are obsolete ("Just use Inventories").
        var idx = stash.IndexVisibleStash;
        var inventories = stash.Inventories;
        var name = inventories != null && idx >= 0 && idx < inventories.Count ? inventories[idx].TabName : null;
        if (!string.IsNullOrWhiteSpace(name)) return "name:" + name;
        return "idx:" + idx;
    }

    /// <summary>Snapshot the currently visible tab. Returns null if nothing is scannable.</summary>
    public TabSnapshot? ScanCurrentTab(DateTime nowUtc)
    {
        var stash = GetVisibleStash();
        var inventory = stash?.VisibleStash;
        var items = inventory?.VisibleInventoryItems;
        if (stash == null || inventory == null || items == null) return null;

        var idx = stash.IndexVisibleStash;
        var inventories = stash.Inventories;
        var tabName = inventories != null && idx >= 0 && idx < inventories.Count ? inventories[idx].TabName : null;
        var snapshot = new TabSnapshot
        {
            Key = ResolveTabKey(stash),
            Name = string.IsNullOrWhiteSpace(tabName) ? $"Tab {idx}" : tabName,
            Type = inventory.InvType.ToString(),
            LastScannedUtc = nowUtc,
            Items = new List<ItemSnapshot>(),
        };

        foreach (var slot in items)
        {
            try
            {
                var entity = slot?.Item;
                if (entity == null || !entity.IsValid) continue;

                var totalEx = _bridge.ExaltedValueOfStack(entity);
                var stackSize = entity.TryGetComponent<Stack>(out var stack) ? Math.Max(1, stack.Size) : 1;
                var baseType = _gc.Files.BaseItemTypes.Translate(entity.Path);
                var baseName = baseType?.BaseName ?? entity.Path;

                string display = baseName;
                string groupKey = baseName;
                if (entity.TryGetComponent<Mods>(out var mods) && !string.IsNullOrEmpty(mods.UniqueName))
                {
                    display = mods.UniqueName;
                    groupKey = mods.UniqueName;
                }

                snapshot.Items.Add(new ItemSnapshot
                {
                    DisplayName = display,
                    GroupKey = groupKey,
                    StackSize = stackSize,
                    TotalValueEx = totalEx,
                });
            }
            catch (Exception ex)
            {
                _logError($"error reading stash item: {ex.Message}");
            }
        }

        return snapshot;
    }
}
