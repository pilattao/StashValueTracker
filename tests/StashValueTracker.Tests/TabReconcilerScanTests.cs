using System;
using System.Collections.Generic;
using System.Linq;
using StashValueTracker.Model;
using StashValueTracker.Tabs;
using Xunit;

namespace StashValueTracker.Tests;

public class TabReconcilerScanTests
{
    private static List<ItemSnapshot> Content(string group, int stack) =>
        new() { new ItemSnapshot { DisplayName = group, GroupKey = group, StackSize = stack, TotalValueEx = 1 } };

    private static TabSnapshot ScanInput(string name, int idx, List<ItemSnapshot> items) =>
        new() { Name = name, Type = "Quad", VisibleIndex = idx, Items = items, LastScannedUtc = new DateTime(2026, 6, 19) };

    private static Func<string> Keys(params string[] ids)
    {
        var q = new Queue<string>(ids);
        return () => q.Dequeue();
    }

    [Fact]
    public void First_scan_of_placeholder_fills_content_keeps_key()
    {
        var placeholder = new TabSnapshot { Key = "k1", Name = "Loot", ColorArgb = 7, TabType = "Map", Scanned = false, Items = new() };
        var stored = new List<TabSnapshot> { placeholder };

        TabReconciler.RecordScan(stored, ScanInput("Loot", 3, Content("Divine Orb", 2)), Keys());

        var t = Assert.Single(stored);
        Assert.Equal("k1", t.Key);
        Assert.True(t.Scanned);
        Assert.Equal(7, t.ColorArgb);            // colour preserved from placeholder
        Assert.Equal(3, t.VisibleIndex);
        Assert.Equal(2, Assert.Single(t.Items).StackSize);
        Assert.Equal(TabFingerprint.Compute(Content("Divine Orb", 2)), t.Fingerprint);
    }

    [Fact]
    public void Triple_change_reunites_orphan_by_fingerprint_and_drops_placeholder()
    {
        var fp = TabFingerprint.Compute(Content("Mirror", 1));
        var orphan = new TabSnapshot { Key = "korphan", Name = "OldName", ColorArgb = 1, TabType = "Normal",
                                       VisibleIndex = 1, Scanned = true, Fingerprint = fp, Items = Content("Mirror", 1) };
        var placeholder = new TabSnapshot { Key = "kplace", Name = "NewName", ColorArgb = 9, TabType = "Map",
                                            VisibleIndex = 8, Scanned = false, Items = new() };
        var stored = new List<TabSnapshot> { orphan, placeholder };

        TabReconciler.RecordScan(stored, ScanInput("NewName", 8, Content("Mirror", 1)), Keys());

        var t = Assert.Single(stored);                  // placeholder dropped
        Assert.Equal("korphan", t.Key);                 // orphan's identity/history survives
        Assert.Equal("NewName", t.Name);
        Assert.True(t.Scanned);
    }

    [Fact]
    public void Identical_content_in_two_orphans_does_not_reunite()
    {
        var fp = TabFingerprint.Compute(Content("Chaos", 1));
        var a = new TabSnapshot { Key = "ka", Name = "EmptyA", Scanned = true, Fingerprint = fp, Items = Content("Chaos", 1) };
        var b = new TabSnapshot { Key = "kb", Name = "EmptyB", Scanned = true, Fingerprint = fp, Items = Content("Chaos", 1) };
        var stored = new List<TabSnapshot> { a, b };

        // Scanning a third differently-named tab with the same fingerprint must NOT merge into a or b.
        TabReconciler.RecordScan(stored, ScanInput("EmptyC", 2, Content("Chaos", 1)), Keys("id:c"));

        Assert.Equal(3, stored.Count);
        Assert.Contains(stored, t => t.Key == "id:c" && t.Name == "EmptyC");
    }

    [Fact]
    public void Rescan_updates_existing_by_name()
    {
        var existing = new TabSnapshot { Key = "k1", Name = "Loot", Scanned = true, Items = Content("Divine Orb", 2),
                                         Fingerprint = TabFingerprint.Compute(Content("Divine Orb", 2)) };
        var stored = new List<TabSnapshot> { existing };

        TabReconciler.RecordScan(stored, ScanInput("Loot", 0, Content("Divine Orb", 5)), Keys());

        var t = Assert.Single(stored);
        Assert.Equal("k1", t.Key);
        Assert.Equal(5, Assert.Single(t.Items).StackSize);
        Assert.Equal(TabFingerprint.Compute(Content("Divine Orb", 5)), t.Fingerprint);
    }

    [Fact]
    public void Unknown_tab_with_no_match_is_created()
    {
        var stored = new List<TabSnapshot>();
        TabReconciler.RecordScan(stored, ScanInput("Solo", 1, Content("Exalted Orb", 4)), Keys("id:new"));
        var t = Assert.Single(stored);
        Assert.Equal("id:new", t.Key);
        Assert.True(t.Scanned);
        Assert.Equal(4, Assert.Single(t.Items).StackSize);
    }

    [Fact]
    public void Rescan_does_not_hijack_a_tab_with_the_same_fingerprint()
    {
        // Two scanned tabs with identical content (same fingerprint). Rescanning one must update
        // that one, never reroute its scan into the other.
        var fp = TabFingerprint.Compute(Content("Same", 1));
        var currency = new TabSnapshot { Key = "kcur", Name = "Currency", Scanned = true, Fingerprint = fp, Items = Content("Same", 1) };
        var dump     = new TabSnapshot { Key = "kdump", Name = "Dump",     Scanned = true, Fingerprint = fp, Items = Content("Same", 1) };
        var stored = new List<TabSnapshot> { currency, dump };

        TabReconciler.RecordScan(stored, ScanInput("Currency", 0, Content("Same", 1)), Keys());

        Assert.Equal(2, stored.Count);
        Assert.Contains(stored, t => t.Key == "kcur" && t.Name == "Currency");
        Assert.Contains(stored, t => t.Key == "kdump" && t.Name == "Dump");
    }
}
