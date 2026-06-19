namespace StashValueTracker.Tabs;

/// <summary>A live stash tab as read from ServerData.PlayerStashTabs.</summary>
public sealed class TabRosterEntry
{
    public string Name { get; init; } = "";
    public int ColorArgb { get; init; }
    public string TabType { get; init; } = "";
    public int VisibleIndex { get; init; }
}
