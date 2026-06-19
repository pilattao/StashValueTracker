using System;
using System.Collections.Generic;

namespace StashValueTracker.Model;

public sealed class ItemSnapshot
{
    public string DisplayName { get; set; } = "";
    public string GroupKey { get; set; } = "";
    public int StackSize { get; set; }
    public double TotalValueEx { get; set; }   // exalted value of the WHOLE stack, at scan time
}

public sealed class TabSnapshot
{
    public string Key { get; set; } = "";      // opaque identity (GUID for new tabs; legacy name-key preserved)
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";      // scan-time inventory type (existing)
    public string TabType { get; set; } = "";   // roster tab type (for signature matching)
    public int ColorArgb { get; set; }          // ServerStashTab.Color2.ToArgb()
    public int VisibleIndex { get; set; }
    public long Fingerprint { get; set; }
    public bool Scanned { get; set; }
    public DateTime LastScannedUtc { get; set; }
    public List<ItemSnapshot> Items { get; set; } = new();
}

public sealed class StashSnapshot
{
    public string League { get; set; } = "";
    public List<TabSnapshot> Tabs { get; set; } = new();
}
