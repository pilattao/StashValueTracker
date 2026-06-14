using ExileCore2.Shared.Attributes;
using ExileCore2.Shared.Interfaces;
using ExileCore2.Shared.Nodes;

namespace StashValueTracker;

public class Settings : ISettings
{
    public ToggleNode Enable { get; set; } = new(true);

    [Menu("Show value window", "Open/close the aggregated stash value window.")]
    public ToggleNode ShowWindow { get; set; } = new(false);

    [Menu("Scan debounce (ms)", "How long a tab must stay open before it is scanned.")]
    public RangeNode<int> ScanDebounceMs { get; set; } = new(300, 0, 2000);
}
