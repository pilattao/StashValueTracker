using System;
using System.Collections.Generic;
using System.Linq;
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

    /// <summary>Total item count in the open tab (for change detection): sub-inventory contents when
    /// present, else the flat list. 0 if none.</summary>
    public int CurrentItemCount(StashElement stash)
    {
        var vis = stash?.VisibleStash;
        if (vis == null) return 0;

        var subs = vis.SubInventories;
        if (subs != null && subs.Count > 0)
        {
            var sum = 0;
            var any = false;
            for (var i = 0; i < subs.Count; i++)
            {
                var c = subs[i]?.VisibleInventoryItems?.Count ?? 0;
                if (c > 0) { sum += c; any = true; }
            }
            if (any) return sum;
        }

        return vis.VisibleInventoryItems?.Count ?? 0;
    }

    /// <summary>Diagnostic dump of the visible stash's nesting structure (for fixing sub-tab support).</summary>
    public string DescribeVisibleStash(StashElement stash)
    {
        try
        {
            var idx = stash.IndexVisibleStash;
            var inv = stash.Inventories;
            var tabName = inv != null && idx >= 0 && idx < inv.Count ? inv[idx].TabName : "<n/a>";
            var vis = stash.VisibleStash;
            if (vis == null) return $"idx={idx} name='{tabName}' VisibleStash=null Inventories={inv?.Count.ToString() ?? "null"}";

            var subs = vis.SubInventories;
            var subCounts = subs == null
                ? "null"
                : "[" + string.Join(",", System.Linq.Enumerable.Range(0, subs.Count)
                    .Select(i => subs[i]?.VisibleInventoryItems?.Count.ToString() ?? "null")) + "]";

            return $"idx={idx} name='{tabName}' InvType={vis.InvType} IsNested={vis.IsNestedInventory} " +
                   $"NestedVisibleIdx={(vis.NestedVisibleInventoryIndex.HasValue ? vis.NestedVisibleInventoryIndex.Value.ToString() : "null")} " +
                   $"VisItems={vis.VisibleInventoryItems?.Count.ToString() ?? "null"} " +
                   $"SubInventories={subs?.Count.ToString() ?? "null"} subItemCounts={subCounts}";
        }
        catch (Exception ex)
        {
            return $"DescribeVisibleStash error: {ex.Message}";
        }
    }

    /// <summary>Opaque tab identity: the tab name (survives reorder), falling back to the index.</summary>
    public string ResolveTabKey(StashElement stash)
    {
        var idx = stash.IndexVisibleStash;
        var inventories = stash.Inventories;
        var name = inventories != null && idx >= 0 && idx < inventories.Count ? inventories[idx].TabName : null;
        return !string.IsNullOrWhiteSpace(name) ? "name:" + name : "idx:" + idx;
    }

    /// <summary>Snapshot the visible tab as a single entry. Special "grid" stashes (currency, runes,
    /// essence, …) expose their contents through SubInventories — flatten those; otherwise use the
    /// flat VisibleInventoryItems.</summary>
    public IReadOnlyList<TabSnapshot> ScanCurrentTab(DateTime nowUtc)
    {
        var stash = GetVisibleStash();
        var visible = stash?.VisibleStash;
        if (stash == null || visible == null) return Array.Empty<TabSnapshot>();

        var idx = stash.IndexVisibleStash;
        var inventories = stash.Inventories;
        var rawName = inventories != null && idx >= 0 && idx < inventories.Count ? inventories[idx].TabName : null;
        var key = !string.IsNullOrWhiteSpace(rawName) ? "name:" + rawName : "idx:" + idx;
        var name = !string.IsNullOrWhiteSpace(rawName) ? rawName : $"Tab {idx}";

        var items = CollectItems(visible);
        return new[] { BuildSnapshot(key, name, null, visible.InvType.ToString(), items, nowUtc) };
    }

    /// <summary>All items in the visible stash: sub-inventory contents when present (covers grid
    /// stashes whose flat VisibleInventoryItems is empty), else the flat list.</summary>
    private static List<NormalInventoryItem> CollectItems(Inventory visible)
    {
        var result = new List<NormalInventoryItem>();

        var subs = visible.SubInventories;
        if (subs != null && subs.Count > 0)
        {
            var any = false;
            for (var i = 0; i < subs.Count; i++)
            {
                var subItems = subs[i]?.VisibleInventoryItems;
                if (subItems != null && subItems.Count > 0)
                {
                    result.AddRange(subItems);
                    any = true;
                }
            }
            if (any) return result;   // grid stash — items live in the sub-inventories
        }

        var flat = visible.VisibleInventoryItems;
        if (flat != null) result.AddRange(flat);
        return result;
    }

    private TabSnapshot BuildSnapshot(string key, string name, string? parentName, string type,
                                      System.Collections.Generic.IList<NormalInventoryItem>? items, DateTime nowUtc)
    {
        var snapshot = new TabSnapshot
        {
            Key = key, Name = name, ParentName = parentName, Type = type,
            LastScannedUtc = nowUtc, Items = new List<ItemSnapshot>(),
        };

        foreach (var slot in items ?? System.Array.Empty<NormalInventoryItem>())
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
                    DisplayName = display, GroupKey = groupKey, StackSize = stackSize, TotalValueEx = totalEx,
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
