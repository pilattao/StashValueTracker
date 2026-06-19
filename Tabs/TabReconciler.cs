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

    /// <summary>Apply a roster match to the stored list: refresh matched tabs, add grey placeholders
    /// for new tabs, prune deleted tabs (only when the roster is stable and no unexplained new tab is
    /// present — keeping the fingerprint reunion window open). Returns true if anything changed.</summary>
    public static bool ApplyRoster(List<TabSnapshot> stored, IReadOnlyList<TabRosterEntry> roster,
                                   bool rosterStable, Func<string> newKey)
    {
        if (stored == null) return false;
        roster ??= Array.Empty<TabRosterEntry>();

        var outcome = Match(roster, stored);
        var changed = false;

        foreach (var m in outcome.Matches)
        {
            if (m.Stored != null)
            {
                if (m.Stored.Name != m.Live.Name) { m.Stored.Name = m.Live.Name; changed = true; }
                if (m.Stored.ColorArgb != m.Live.ColorArgb) { m.Stored.ColorArgb = m.Live.ColorArgb; changed = true; }
                if (m.Stored.TabType != m.Live.TabType) { m.Stored.TabType = m.Live.TabType; changed = true; }
                if (m.Stored.VisibleIndex != m.Live.VisibleIndex) { m.Stored.VisibleIndex = m.Live.VisibleIndex; changed = true; }
            }
            else
            {
                stored.Add(new TabSnapshot
                {
                    Key = newKey(),
                    Name = m.Live.Name,
                    ColorArgb = m.Live.ColorArgb,
                    TabType = m.Live.TabType,
                    VisibleIndex = m.Live.VisibleIndex,
                    Scanned = false,
                    Items = new List<ItemSnapshot>(),
                });
                changed = true;
            }
        }

        var hasNew = outcome.Matches.Any(m => m.Kind == TabMatchKind.New);
        if (rosterStable && !hasNew)
        {
            foreach (var miss in outcome.Missing)
                if (stored.Remove(miss)) changed = true;
        }

        return changed;
    }

    /// <summary>Write a freshly-scanned tab's contents into the right stored tab. Fingerprint reunion
    /// (name changed, unique content match) fires only for an orphan whose name is genuinely gone from
    /// the live roster — otherwise two distinct live tabs with identical content (e.g. two empty tabs)
    /// would collapse. A scanned name-owner short-circuits to a plain rescan. Falls back to the
    /// placeholder, else creates a new tab.</summary>
    public static void RecordScan(List<TabSnapshot> stored, TabSnapshot scanned, Func<string> newKey, ISet<string> liveNames)
    {
        if (stored == null || scanned == null) return;
        var fp = TabFingerprint.Compute(scanned.Items);

        var nameMatch = stored.FirstOrDefault(t => NameEq(t.Name, scanned.Name));
        TabSnapshot target;

        if (nameMatch != null && nameMatch.Scanned)
        {
            // A scanned tab already owns this name → normal rescan; never reunite/hijack.
            target = nameMatch;
        }
        else
        {
            // Reunion only with a unique scanned orphan whose name is genuinely gone from the live
            // roster (a real rename-away). Without liveNames we cannot tell a rename from a distinct
            // identical-content tab, so we do not reunite.
            TabSnapshot? reunion = null;
            if (liveNames != null && liveNames.Count > 0)
            {
                var cands = stored
                    .Where(t => t.Scanned && t.Fingerprint == fp
                                && !NameEq(t.Name, scanned.Name)
                                && !liveNames.Contains(t.Name))
                    .ToList();
                if (cands.Count == 1) reunion = cands[0];
            }

            if (reunion != null)
            {
                target = reunion;
                if (nameMatch != null) stored.Remove(nameMatch);   // drop the empty placeholder
            }
            else if (nameMatch != null)
            {
                target = nameMatch;                                // unscanned placeholder
            }
            else
            {
                target = new TabSnapshot { Key = newKey(), ColorArgb = scanned.ColorArgb, TabType = scanned.TabType };
                stored.Add(target);
            }
        }

        target.Name = scanned.Name;
        target.Type = scanned.Type;
        target.VisibleIndex = scanned.VisibleIndex;
        target.Items = scanned.Items ?? new List<ItemSnapshot>();
        target.Fingerprint = fp;
        target.Scanned = true;
        target.LastScannedUtc = scanned.LastScannedUtc;
    }
}
