using System.Collections.Generic;
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

    [Menu("Auto-open/close with stash", "Open the value window when you open your stash, and close it when you close the stash.")]
    public ToggleNode OpenCloseWithStash { get; set; } = new(false);

    [Menu("Scan debounce (ms)", "How long a tab must stay open before it is scanned.")]
    public RangeNode<int> ScanDebounceMs { get; set; } = new(300, 0, 2000);

    [Menu("Toggle window hotkey", "Press to open/close the value window. Bind it here.")]
    public HotkeyNodeV2 ToggleWindowHotkey { get; set; } = new HotkeyNodeV2(Keys.None);

    // Persisted width of the tab panel (the draggable vertical splitter). No [Menu] — hidden but saved.
    public RangeNode<float> TabPanelWidth { get; set; } = new(240f, 120f, 800f);

    // Persisted set of tab keys the user has unchecked in the filter. No [Menu] — hidden but saved.
    public List<string> ExcludedTabKeys { get; set; } = new();
}
