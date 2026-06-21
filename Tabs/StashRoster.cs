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

            var result = new List<TabRosterEntry>(tabs.Count);
            foreach (var t in tabs)
            {
                if (t == null) continue;
                result.Add(new TabRosterEntry
                {
                    Name = t.Name ?? "",
                    ColorArgb = t.Color2.ToArgb(),
                    TabType = t.TabType.ToString(),
                    VisibleIndex = t.VisibleIndex,
                });
            }
            return result;
        }
        catch (Exception ex)
        {
            _logError($"error reading stash roster: {ex.Message}");
            return Array.Empty<TabRosterEntry>();
        }
    }

    /// <summary>Cheap signature for stability detection across ticks.</summary>
    public static string RosterSignature(IReadOnlyList<TabRosterEntry> roster) =>
        roster == null ? "" : string.Join("|", roster.Select(r => $"{r.Name}:{r.VisibleIndex}:{r.ColorArgb}:{r.TabType}"));

    // DEBUG (temporary): raw dump of PlayerStashTabs including fields TabRosterEntry does not carry.
    public IReadOnlyList<string> DebugRawLines()
    {
        try
        {
            var tabs = _gc?.IngameState?.ServerData?.PlayerStashTabs;
            if (tabs == null) return new[] { "PlayerStashTabs: null" };
            var lines = new List<string> { $"PlayerStashTabs.Count={tabs.Count}" };
            for (var i = 0; i < tabs.Count; i++)
            {
                var t = tabs[i];
                if (t == null) { lines.Add($"  [{i}] <null>"); continue; }
                lines.Add($"  [{i}] name='{t.Name}' nameOld='{t.NameOld}' idx={t.VisibleIndex} color=0x{t.Color2.ToArgb():X8} type={t.TabType} flags={t.Flags} member={t.MemberFlags} officer={t.OfficerFlags}");
            }
            return lines;
        }
        catch (Exception ex) { return new[] { "DebugRawLines error: " + ex.Message }; }
    }
}
