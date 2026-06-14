using System.Collections.Generic;
using System.IO;
using StashValueTracker.Model;
using StashValueTracker.Storage;
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
    public void ForgetTab_removes_by_key()
    {
        var store = new SnapshotStore(TempDir());
        store.LoadLeague("Standard");
        store.UpsertTab(SampleTab("name:A"));
        store.UpsertTab(SampleTab("name:B"));
        store.ForgetTab("name:A");
        var tab = Assert.Single(store.Tabs);
        Assert.Equal("name:B", tab.Key);
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
