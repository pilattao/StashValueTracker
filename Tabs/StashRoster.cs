using System;
using System.Collections.Generic;
using System.Linq;
using ExileCore2;

namespace StashValueTracker.Tabs;

/// <summary>Reads the authoritative tab roster from ServerData.PlayerStashTabs (names, colours,
/// types, order) — available without opening each tab.</summary>
public sealed class StashRoster
{
    private readonly GameController _gc;
    private readonly Action<string> _logError;

    public StashRoster(GameController gc, Action<string> logError)
    {
        _gc = gc;
        _logError = logError;
    }

    public IReadOnlyList<TabRosterEntry> Read()
    {
        try
        {
            var tabs = _gc?.IngameState?.ServerData?.PlayerStashTabs;
            if (tabs == null || tabs.Count == 0) return Array.Empty<TabRosterEntry>();

            var raw = new List<TabRosterEntry>(tabs.Count);
            foreach (var t in tabs)
            {
                if (t == null) continue;
                raw.Add(new TabRosterEntry
                {
                    Name = t.Name ?? "",
                    ColorArgb = t.Color2.ToArgb(),
                    TabType = t.TabType.ToString(),
                    VisibleIndex = t.VisibleIndex,
                });
            }

            // PlayerStashTabs also carries other players' public tabs (party/trade), which collide on
            // VisibleIndex or fall outside the own range — keep only the local player's own tabs, using
            // the stash UI's own-tab count as the authority.
            return TabRosterFilter.SelectOwn(raw, OwnTabCount());
        }
        catch (Exception ex)
        {
            _logError($"error reading stash roster: {ex.Message}");
            return Array.Empty<TabRosterEntry>();
        }
    }

    /// <summary>Number of tabs the local player owns, from the stash UI (0 if the panel is not loaded).</summary>
    private int OwnTabCount()
    {
        try
        {
            var stash = _gc?.IngameState?.IngameUi?.StashElement;
            if (stash == null) return 0;
            var n = stash.AllStashNames?.Count ?? 0;
            if (n <= 0) n = stash.Inventories?.Count ?? 0;
            if (n <= 0) n = (int)stash.TotalStashes;
            return n;
        }
        catch { return 0; }
    }

    /// <summary>Cheap signature for stability detection across ticks.</summary>
    public static string RosterSignature(IReadOnlyList<TabRosterEntry> roster) =>
        roster == null ? "" : string.Join("|", roster.Select(r => $"{r.Name}:{r.VisibleIndex}:{r.ColorArgb}:{r.TabType}"));
}
