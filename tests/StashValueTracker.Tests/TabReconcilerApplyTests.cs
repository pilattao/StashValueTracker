using System;
using System.Collections.Generic;
using System.Linq;
using StashValueTracker.Model;
using StashValueTracker.Tabs;
using Xunit;

namespace StashValueTracker.Tests;

public class TabReconcilerApplyTests
{
    private static TabRosterEntry Live(string name, int color = 0, string type = "Normal", int idx = 0) =>
        new() { Name = name, ColorArgb = color, TabType = type, VisibleIndex = idx };

    private static TabSnapshot Scanned(string key, string name, int color = 0, string type = "Normal", int idx = 0) =>
        new()
        {
            Key = key, Name = name, ColorArgb = color, TabType = type, VisibleIndex = idx,
            Scanned = true, Items = new List<ItemSnapshot> { new() { GroupKey = "X", StackSize = 1, TotalValueEx = 1 } },
        };

    private static Func<string> Keys(params string[] ids)
    {
        var q = new Queue<string>(ids);
        return () => q.Dequeue();
    }

    [Fact]
    public void New_tab_added_as_grey_placeholder()
    {
        var stored = new List<TabSnapshot>();
        var changed = TabReconciler.ApplyRoster(stored, new[] { Live("Fresh", color: 7, type: "Currency", idx: 4) },
            rosterStable: true, newKey: Keys("id:1"));
        Assert.True(changed);
        var t = Assert.Single(stored);
        Assert.Equal("id:1", t.Key);
        Assert.Equal("Fresh", t.Name);
        Assert.Equal(7, t.ColorArgb);
        Assert.Equal("Currency", t.TabType);
        Assert.Equal(4, t.VisibleIndex);
        Assert.False(t.Scanned);
        Assert.Empty(t.Items);
    }

    [Fact]
    public void Existing_by_name_keeps_key_and_items_but_refreshes_meta()
    {
        var stored = new List<TabSnapshot> { Scanned("k1", "Currency", color: 1, idx: 0) };
        TabReconciler.ApplyRoster(stored, new[] { Live("Currency", color: 9, idx: 5) },
            rosterStable: true, newKey: Keys());
        var t = Assert.Single(stored);
        Assert.Equal("k1", t.Key);
        Assert.Single(t.Items);          // content untouched
        Assert.Equal(9, t.ColorArgb);    // meta refreshed
        Assert.Equal(5, t.VisibleIndex);
    }

    [Fact]
    public void Pure_rename_updates_name_keeps_key_and_items()
    {
        var stored = new List<TabSnapshot> { Scanned("k1", "OldName", color: 5, type: "Normal", idx: 3) };
        TabReconciler.ApplyRoster(stored, new[] { Live("NewName", color: 5, type: "Normal", idx: 3) },
            rosterStable: true, newKey: Keys());
        var t = Assert.Single(stored);
        Assert.Equal("k1", t.Key);
        Assert.Equal("NewName", t.Name);
        Assert.Single(t.Items);
    }

    [Fact]
    public void Deleted_tab_pruned_when_stable_and_no_new()
    {
        var stored = new List<TabSnapshot> { Scanned("k1", "Gone") };
        var changed = TabReconciler.ApplyRoster(stored, new TabRosterEntry[0],
            rosterStable: true, newKey: Keys());
        Assert.True(changed);
        Assert.Empty(stored);
    }

    [Fact]
    public void Deleted_tab_kept_when_unstable()
    {
        var stored = new List<TabSnapshot> { Scanned("k1", "Gone") };
        TabReconciler.ApplyRoster(stored, new TabRosterEntry[0], rosterStable: false, newKey: Keys());
        Assert.Single(stored);
    }

    [Fact]
    public void Missing_kept_while_an_unexplained_new_tab_exists()
    {
        // triple-change reunion window: a New tab is present, so the orphan is retained
        var stored = new List<TabSnapshot> { Scanned("k1", "Orphan", color: 1, type: "Normal", idx: 1) };
        TabReconciler.ApplyRoster(stored, new[] { Live("BrandNew", color: 9, type: "Map", idx: 8) },
            rosterStable: true, newKey: Keys("id:new"));
        Assert.Contains(stored, t => t.Key == "k1");      // orphan kept
        Assert.Contains(stored, t => t.Key == "id:new");  // placeholder added
    }
}
