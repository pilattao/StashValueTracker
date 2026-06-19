using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using StashValueTracker.Model;
using StashValueTracker.Tabs;

namespace StashValueTracker.Storage;

public sealed class SnapshotStore
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private readonly string _dataDir;
    private readonly Action<string>? _logError;
    private StashSnapshot _current = new();

    public SnapshotStore(string dataDir, Action<string>? logError = null)
    {
        _dataDir = dataDir;
        _logError = logError;
    }

    public string League => _current.League;
    public IReadOnlyList<TabSnapshot> Tabs => _current.Tabs;

    public void LoadLeague(string league)
    {
        league ??= "";
        var path = PathFor(league);
        if (!File.Exists(path))
        {
            _current = new StashSnapshot { League = league };
            return;
        }

        try
        {
            var loaded = JsonSerializer.Deserialize<StashSnapshot>(File.ReadAllText(path));
            _current = loaded ?? new StashSnapshot();
            _current.League = league;
            _current.Tabs ??= new List<TabSnapshot>();
            // Back-fill Scanned for snapshots persisted before the field existed: old-format tabs
            // were only ever created by scanning, so a stored tab with items or a scan time was
            // scanned. Genuine new-format placeholders (empty, default time) correctly stay false.
            foreach (var t in _current.Tabs)
                if (!t.Scanned && (t.Items is { Count: > 0 } || t.LastScannedUtc != default))
                    t.Scanned = true;
        }
        catch (Exception ex)
        {
            _logError?.Invoke($"failed to load snapshot for '{league}': {ex.Message}");
            _current = new StashSnapshot { League = league };
        }
    }

    public void UpsertTab(TabSnapshot tab)
    {
        if (tab == null) return;
        _current.Tabs.RemoveAll(t => t.Key == tab.Key);
        _current.Tabs.Add(tab);
    }

    public void ForgetTab(string key) => _current.Tabs.RemoveAll(t => t.Key == key);

    private static string NewKey() => "id:" + Guid.NewGuid().ToString("N");

    /// <summary>Reconcile the live roster into the store. Returns true if anything changed.</summary>
    public bool SyncRoster(IReadOnlyList<TabRosterEntry> roster, bool rosterStable) =>
        TabReconciler.ApplyRoster(_current.Tabs, roster, rosterStable, NewKey);

    /// <summary>Integrate a freshly-scanned tab (resolves identity, reunites renamed tabs).</summary>
    public void RecordScan(TabSnapshot scanned) =>
        TabReconciler.RecordScan(_current.Tabs, scanned, NewKey);

    /// <summary>Forget: clear a tab's scanned content but keep the row (the roster still lists it).</summary>
    public void ResetTab(string key)
    {
        var t = _current.Tabs.Find(x => x.Key == key);
        if (t == null) return;
        t.Items = new List<ItemSnapshot>();
        t.Fingerprint = 0;
        t.Scanned = false;
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(_dataDir);
            var path = PathFor(_current.League);
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(_current, JsonOpts));
            File.Move(tmp, path, overwrite: true);
        }
        catch (Exception ex)
        {
            _logError?.Invoke($"failed to save snapshot: {ex.Message}");
        }
    }

    private string PathFor(string league) => Path.Combine(_dataDir, Sanitize(league) + ".json");

    private static string Sanitize(string s)
    {
        if (string.IsNullOrEmpty(s)) return "__unknown__";
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(s.Length);
        foreach (var c in s) sb.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
        return sb.ToString();
    }
}
