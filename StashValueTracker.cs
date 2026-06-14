using System;
using System.IO;
using ExileCore2;
using StashValueTracker.Pricing;
using StashValueTracker.Scanning;
using StashValueTracker.Storage;
using StashValueTracker.UI;

namespace StashValueTracker;

public class StashValueTracker : BaseSettingsPlugin<Settings>
{
    private const long RescanIntervalMs = 2500;   // re-scan the still-open tab this often
    private const long SaveIntervalMs = 5000;     // debounce disk writes

    private NinjaPricerBridge _bridge = null!;
    private StashScanner _scanner = null!;
    private SnapshotStore _store = null!;
    private ValueWindow _window = null!;

    private string _currentLeague = "";
    private bool _leagueLoaded;
    private bool _stashWasOpen;

    private string? _pendingTabKey;
    private long _pendingSinceMs;
    private long _lastScanMs;
    private int _lastItemCount = -1;

    private bool _dirty;
    private long _lastSaveMs;

    public override bool Initialise()
    {
        _bridge = new NinjaPricerBridge(GameController, msg => LogError($"[bridge] {msg}"));
        _scanner = new StashScanner(GameController, _bridge, msg => LogError($"[scan] {msg}"));
        _store = new SnapshotStore(Path.Combine(DirectoryFullName, "data"), msg => LogError($"[store] {msg}"));
        _window = new ValueWindow();
        // League may be empty at startup; the store is loaded lazily on the first tick with a known league.
        return true;
    }

    public override void Tick()
    {
        if (!Settings.Enable) return;

        var league = ResolveLeague();
        if (string.IsNullOrEmpty(league)) return;   // not in-game yet — don't load or scan

        if (!_leagueLoaded || !string.Equals(league, _currentLeague, StringComparison.OrdinalIgnoreCase))
        {
            FlushIfDirty();                          // persist previous league before switching
            _currentLeague = league;
            _store.LoadLeague(league);
            _leagueLoaded = true;
            _pendingTabKey = null;
        }

        var stash = _scanner.GetVisibleStash();
        if (stash == null)
        {
            if (_stashWasOpen) { FlushIfDirty(); _stashWasOpen = false; }
            _pendingTabKey = null;
            return;
        }
        _stashWasOpen = true;

        var key = _scanner.ResolveTabKey(stash);
        var nowMs = Environment.TickCount64;

        if (key != _pendingTabKey)
        {
            _pendingTabKey = key;
            _pendingSinceMs = nowMs;
            _lastScanMs = 0;
            _lastItemCount = -1;
            return;
        }

        if (nowMs - _pendingSinceMs < Settings.ScanDebounceMs.Value) return;
        if (!_bridge.PricesReady) return;

        var itemCount = _scanner.CurrentItemCount(stash);
        var neverScanned = _lastScanMs == 0;
        var countChanged = itemCount != _lastItemCount;
        var periodic = nowMs - _lastScanMs >= RescanIntervalMs;

        if (neverScanned || countChanged || periodic)
        {
            var snapshot = _scanner.ScanCurrentTab(DateTime.UtcNow);
            if (snapshot != null)
            {
                _store.UpsertTab(snapshot);
                _dirty = true;
                _lastScanMs = nowMs;
                _lastItemCount = itemCount;
            }
        }

        MaybeFlush(nowMs);
    }

    public override void Render()
    {
        if (!Settings.Enable || !Settings.ShowWindow) return;

        var show = true;
        _window.Draw(_store, _bridge.DivinePerExalted, _bridge.IsAvailable, _bridge.PricesReady,
                     () => _dirty = true, ref show);
        if (!show) Settings.ShowWindow.Value = false;
    }

    public override void OnPluginDestroyForHotReload()
    {
        FlushIfDirty();
        base.OnPluginDestroyForHotReload();
    }

    private void MaybeFlush(long nowMs)
    {
        if (_dirty && nowMs - _lastSaveMs >= SaveIntervalMs)
        {
            _store.Save();
            _dirty = false;
            _lastSaveMs = nowMs;
        }
    }

    private void FlushIfDirty()
    {
        if (!_dirty) return;
        _store.Save();
        _dirty = false;
        _lastSaveMs = Environment.TickCount64;
    }

    private string ResolveLeague()
    {
        var raw = GameController?.IngameState?.ServerData?.League;
        return string.IsNullOrWhiteSpace(raw) ? "" : raw;
    }
}
