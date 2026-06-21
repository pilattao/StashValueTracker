using System;
using System.Collections.Generic;

namespace StashValueTracker.Tabs;

/// <summary>Selects the local player's own stash tabs out of the raw ServerData.PlayerStashTabs list.
/// The game streams extra entries into PlayerStashTabs — other players' public tabs (party/trade) —
/// which flood the panel. They cannot be told apart by flags (your own tabs can be Public too), so we
/// rely on the local stash UI as the authority: the player owns exactly <paramref name="ownCount"/>
/// tabs occupying VisibleIndex 0..ownCount-1, while the foreign tabs collide on those indices or fall
/// outside the range. Keep the first entry per in-range VisibleIndex.</summary>
public static class TabRosterFilter
{
    public static IReadOnlyList<TabRosterEntry> SelectOwn(IReadOnlyList<TabRosterEntry> raw, int ownCount)
    {
        if (raw == null || ownCount <= 0) return Array.Empty<TabRosterEntry>();

        var seen = new HashSet<int>();
        var result = new List<TabRosterEntry>(ownCount);
        foreach (var e in raw)
        {
            if (e == null) continue;
            if (e.VisibleIndex < 0 || e.VisibleIndex >= ownCount) continue;   // foreign tab outside the own range
            if (!seen.Add(e.VisibleIndex)) continue;                          // duplicate index -> foreign tab
            result.Add(e);
            if (result.Count == ownCount) break;
        }
        return result;
    }
}
