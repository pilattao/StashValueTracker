using System;
using System.Collections.Generic;
using System.IO;
using StashValueTracker.Model;
using StashValueTracker.Storage;
using StashValueTracker.Tabs;
using Xunit;

namespace StashValueTracker.Tests;

public class SnapshotStoreTests
{
    private static string TempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "svt_test_" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static TabSnapshot SampleTab(string key) => new()
    {
        Key = key, Name = "Currency", Type = "CurrencyStash",
        Items = new List<ItemSnapshot> { new() { DisplayName = "Exalted Orb", GroupKey = "Exalted Orb", StackSize = 5, TotalValueEx = 5 } },
    };

    [Fact]
    public void Save_then_reload_round_trips_tabs()
    {
        var dir = TempDir();
        var store = new SnapshotStore(dir);
        store.LoadLeague("Standard");
        store.UpsertTab(SampleTab("name:Currency"));
        store.Save();

        var reloaded = new SnapshotStore(dir);
        reloaded.LoadLeague("Standard");

        var tab = Assert.Single(reloaded.Tabs);
        Assert.Equal("Currency", tab.Name);
        Assert.Equal("Exalted Orb", Assert.Single(tab.Items).DisplayName);
    }

    [Fact]
    public void Upsert_replaces_tab_with_same_key()
    {
        var store = new SnapshotStore(TempDir());
        store.LoadLeague("Standard");
        store.UpsertTab(SampleTab("name:Currency"));
        store.UpsertTab(new TabSnapshot { Key = "name:Currency", Name = "Renamed" });
        var tab = Assert.Single(store.Tabs);
        Assert.Equal("Renamed", tab.Name);
    }

    [Fact]
    public void ResetTab_clears_content_but_keeps_row()
    {
        var store = new SnapshotStore(TempDir());
        store.LoadLeague("Standard");
        store.UpsertTab(new TabSnapshot
        {
            Key = "id:1", Name = "Currency", Scanned = true, Fingerprint = 99,
            Items = new System.Collections.Generic.List<ItemSnapshot> { new() { GroupKey = "X", StackSize = 1, TotalValueEx = 1 } },
        });
        store.ResetTab("id:1");
        var t = Assert.Single(store.Tabs);
        Assert.Equal("id:1", t.Key);
        Assert.False(t.Scanned);
        Assert.Empty(t.Items);
        Assert.Equal(0, t.Fingerprint);
    }

    [Fact]
    public void SyncRoster_adds_placeholder_and_reports_change()
    {
        var store = new SnapshotStore(TempDir());
        store.LoadLeague("Standard");
        var changed = store.SyncRoster(
            new[] { new TabRosterEntry { Name = "Maps", ColorArgb = 3, TabType = "Map", VisibleIndex = 1 } },
            rosterStable: true);
        Assert.True(changed);
        var t = Assert.Single(store.Tabs);
        Assert.Equal("Maps", t.Name);
        Assert.False(t.Scanned);
        Assert.StartsWith("id:", t.Key);
    }

    [Fact]
    public void RecordScan_fills_the_synced_placeholder()
    {
        var store = new SnapshotStore(TempDir());
        store.LoadLeague("Standard");
        store.SyncRoster(
            new[] { new TabRosterEntry { Name = "Maps", ColorArgb = 3, TabType = "Map", VisibleIndex = 1 } },
            rosterStable: true);
        store.RecordScan(new TabSnapshot
        {
            Name = "Maps", Type = "Quad", VisibleIndex = 1,
            Items = new System.Collections.Generic.List<ItemSnapshot> { new() { GroupKey = "Y", StackSize = 3, TotalValueEx = 9 } },
        });
        var t = Assert.Single(store.Tabs);
        Assert.True(t.Scanned);
        Assert.Equal(3, Assert.Single(t.Items).StackSize);
        Assert.Equal(3, t.ColorArgb);   // colour preserved from the placeholder
    }

    [Fact]
    public void Leagues_are_stored_in_separate_files()
    {
        var dir = TempDir();
        var store = new SnapshotStore(dir);
        store.LoadLeague("Standard");
        store.UpsertTab(SampleTab("name:Currency"));
        store.Save();
        store.LoadLeague("Hardcore");
        Assert.Empty(store.Tabs);
    }

    [Fact]
    public void League_with_invalid_filename_chars_round_trips()
    {
        var dir = TempDir();
        var store = new SnapshotStore(dir);
        store.LoadLeague("HC SSF Mercenaries / R2");
        store.UpsertTab(SampleTab("name:Currency"));
        store.Save();

        var reloaded = new SnapshotStore(dir);
        reloaded.LoadLeague("HC SSF Mercenaries / R2");
        Assert.Single(reloaded.Tabs);
    }

    [Fact]
    public void Legacy_tab_with_items_is_backfilled_as_scanned()
    {
        // Write old-format JSON (no Scanned field) with Items present — simulates a snapshot
        // persisted before the Scanned field was introduced.
        var dir = TempDir();
        var json = """
            {
              "League": "Standard",
              "Tabs": [
                {
                  "Key": "name:Currency",
                  "Name": "Currency",
                  "Type": "CurrencyStash",
                  "TabType": "",
                  "ColorArgb": 0,
                  "VisibleIndex": 0,
                  "Fingerprint": 0,
                  "LastScannedUtc": "0001-01-01T00:00:00",
                  "Items": [
                    { "DisplayName": "Chaos Orb", "GroupKey": "Chaos Orb", "StackSize": 10, "TotalValueEx": 1.0 }
                  ]
                }
              ]
            }
            """;
        File.WriteAllText(Path.Combine(dir, "Standard.json"), json);

        var store = new SnapshotStore(dir);
        store.LoadLeague("Standard");

        var tab = Assert.Single(store.Tabs);
        Assert.True(tab.Scanned, "Legacy tab with items must be back-filled as Scanned=true");
    }

    [Fact]
    public void New_format_placeholder_stays_not_scanned_after_round_trip()
    {
        // A placeholder tab created by SyncRoster has empty Items and default LastScannedUtc;
        // the back-fill must not promote it to Scanned=true.
        var dir = TempDir();
        var store = new SnapshotStore(dir);
        store.LoadLeague("Standard");
        store.SyncRoster(
            new[] { new TabRosterEntry { Name = "Maps", ColorArgb = 0, TabType = "Map", VisibleIndex = 0 } },
            rosterStable: true);
        store.Save();

        var reloaded = new SnapshotStore(dir);
        reloaded.LoadLeague("Standard");

        var tab = Assert.Single(reloaded.Tabs);
        Assert.False(tab.Scanned, "New-format placeholder must remain Scanned=false after reload");
        Assert.Empty(tab.Items);
        Assert.Equal(default(DateTime), tab.LastScannedUtc);
    }

    [Fact]
    public void Corrupt_file_loads_as_empty_without_throwing()
    {
        var dir = TempDir();
        var store0 = new SnapshotStore(dir);
        store0.LoadLeague("Standard");
        store0.UpsertTab(SampleTab("name:Currency"));
        store0.Save();
        var file = Directory.GetFiles(dir, "*.json")[0];
        File.WriteAllText(file, "{ this is not valid json");

        var store = new SnapshotStore(dir);
        store.LoadLeague("Standard");
        Assert.Empty(store.Tabs);
        Assert.Equal("Standard", store.League);
    }
}
