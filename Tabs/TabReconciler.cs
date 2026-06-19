using System;
using System.Collections.Generic;
using System.Linq;
using StashValueTracker.Model;

namespace StashValueTracker.Tabs;

public enum TabMatchKind { Name, Signature, New }

public sealed class TabMatch
{
    public TabRosterEntry Live { get; init; } = null!;
    public TabSnapshot? Stored { get; init; }
    public TabMatchKind Kind { get; init; }
}

public sealed class ReconcileOutcome
{
    public IReadOnlyList<TabMatch> Matches { get; init; } = Array.Empty<TabMatch>();
    public IReadOnlyList<TabSnapshot> Missing { get; init; } = Array.Empty<TabSnapshot>();
}

public static partial class TabReconciler
{
    private static bool NameEq(string a, string b) =>
        string.Equals(a ?? "", b ?? "", StringComparison.OrdinalIgnoreCase);

    /// <summary>Map a live roster onto stored tabs: exact name, then unique signature, else New.
    /// Pure — performs no mutation.</summary>
    public static ReconcileOutcome Match(IReadOnlyList<TabRosterEntry> roster, IReadOnlyList<TabSnapshot> stored)
    {
        roster ??= Array.Empty<TabRosterEntry>();
        stored ??= Array.Empty<TabSnapshot>();

        var claimed = new HashSet<TabSnapshot>();
        var resolved = new TabMatch[roster.Count];
        var pending = new List<int>();

        // Tier 1: exact name.
        for (var i = 0; i < roster.Count; i++)
        {
            var live = roster[i];
            var s = stored.FirstOrDefault(x => !claimed.Contains(x) && NameEq(x.Name, live.Name));
            if (s != null)
            {
                claimed.Add(s);
                resolved[i] = new TabMatch { Live = live, Stored = s, Kind = TabMatchKind.Name };
            }
            else pending.Add(i);
        }

        // Tier 2: unique signature among remaining orphans.
        foreach (var i in pending)
        {
            var live = roster[i];
            var cands = stored
                .Where(x => !claimed.Contains(x)
                            && string.Equals(x.TabType ?? "", live.TabType ?? "", StringComparison.Ordinal)
                            && (x.ColorArgb == live.ColorArgb || x.VisibleIndex == live.VisibleIndex))
                .ToList();
            if (cands.Count == 1)
            {
                claimed.Add(cands[0]);
                resolved[i] = new TabMatch { Live = live, Stored = cands[0], Kind = TabMatchKind.Signature };
            }
            else
            {
                resolved[i] = new TabMatch { Live = live, Stored = null, Kind = TabMatchKind.New };
            }
        }

        var missing = stored.Where(x => !claimed.Contains(x)).ToList();
        return new ReconcileOutcome { Matches = resolved, Missing = missing };
    }
}
