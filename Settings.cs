using System.Windows.Forms;
using ExileCore2.Shared.Attributes;
using ExileCore2.Shared.Interfaces;
using ExileCore2.Shared.Nodes;

namespace StashValueTracker;

public class Settings : ISettings
{
    public ToggleNode Enable { get; set; } = new(true);

    [Menu("Show value window", "Open/close the aggregated stash value window.")]
    public ToggleNode ShowWindow { get; set; } = new(false);

    [Menu("Auto-open with stash", "Open the value window automatically when you open your stash.")]
    public ToggleNode AutoOpenWithStash { get; set; } = new(false);

    [Menu("Scan debounce (ms)", "How long a tab must stay open before it is scanned.")]
    public RangeNode<int> ScanDebounceMs { get; set; } = new(300, 0, 2000);

    [Menu("Toggle window hotkey", "Press to open/close the value window. Bind it here.")]
    public HotkeyNodeV2 ToggleWindowHotkey { get; set; } = new HotkeyNodeV2(Keys.None);

    [Menu("Auto-refresh open tab", "ON: re-scan the open tab periodically and when its item count changes. OFF: scan only on tab/sub-tab change.")]
    public ToggleNode AutoRefreshOpenTab { get; set; } = new(true);

    [Menu("Re-scan interval (ms)", "How often the open tab re-scans while auto-refresh is on.")]
    public RangeNode<int> RescanIntervalMs { get; set; } = new(2500, 500, 10000);
}
