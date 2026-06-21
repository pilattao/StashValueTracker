using System.Collections.Generic;
using System.Linq;
using StashValueTracker.Tabs;
using Xunit;

namespace StashValueTracker.Tests;

public class TabRosterFilterTests
{
    private static TabRosterEntry E(string name, int idx) =>
        new() { Name = name, VisibleIndex = idx, ColorArgb = 0, TabType = "" };

    // The exact PlayerStashTabs payload captured in-game: 27 own tabs (VisibleIndex 0..26) followed by
    // 24 foreign public tabs whose indices collide with the own range or fall outside it (e.g. 96).
    private static List<TabRosterEntry> RealDump()
    {
        var own = new[]
        {
            E("M", 3), E("27", 23), E("15", 17), E("28", 24), E("1", 1), E("1", 13), E("2", 0), E("2", 14),
            E("SKINS", 26), E("bows", 18), E("A", 5), E("E", 8), E("R", 12), E("B", 9), E("F", 4), E("V", 7),
            E("10", 15), E("21", 20), E("G", 11), E("22", 21), E("MY_HOARD", 25), E("23", 22), E("W", 6),
            E("C", 2), E("D", 10), E("13", 16), E("25", 19),
        };
        var foreign = new[]
        {
            E("1", 3), E("1", 2), E("1", 1), E("1", 8), E("1", 2), E("1", 20), E("1", 96), E("1", 1),
            E("2", 17), E("1", 9), E("1", 18), E("1", 6), E("1", 12), E("1", 10), E("1", 24), E("1", 2),
            E("1", 4), E("1", 17), E("1", 6), E("1", 10), E("1", 1), E("1", 4), E("1", 7), E("1", 2),
        };
        return own.Concat(foreign).ToList();
    }

    [Fact]
    public void Keeps_exactly_the_own_tabs_and_drops_foreign_phantoms()
    {
        var result = TabRosterFilter.SelectOwn(RealDump(), ownCount: 27);

        Assert.Equal(27, result.Count);
        // VisibleIndex forms the complete own range 0..26 with no duplicates.
        Assert.Equal(Enumerable.Range(0, 27), result.Select(r => r.VisibleIndex).OrderBy(i => i));
        // No out-of-range phantom (idx 96) survives.
        Assert.DoesNotContain(result, r => r.VisibleIndex >= 27);
    }

    [Fact]
    public void Own_tab_wins_over_a_foreign_tab_sharing_its_index()
    {
        var result = TabRosterFilter.SelectOwn(RealDump(), ownCount: 27);
        // Index 2 is the own Currency tab "C"; the foreign "1"@2 entries must not replace it.
        Assert.Equal("C", result.Single(r => r.VisibleIndex == 2).Name);
        // Both own duplicate-named tabs survive: "1"@1 and "1"@13, "2"@0 and "2"@14.
        Assert.Equal("1", result.Single(r => r.VisibleIndex == 1).Name);
        Assert.Equal("1", result.Single(r => r.VisibleIndex == 13).Name);
        Assert.Equal("2", result.Single(r => r.VisibleIndex == 0).Name);
        Assert.Equal("2", result.Single(r => r.VisibleIndex == 14).Name);
    }

    [Fact]
    public void Keeps_first_entry_per_index()
    {
        // Documents the own-tabs-come-first assumption: among entries sharing an index, the first wins.
        var raw = new List<TabRosterEntry> { E("first", 0), E("second", 0) };
        var result = TabRosterFilter.SelectOwn(raw, ownCount: 1);
        Assert.Equal("first", Assert.Single(result).Name);
    }

    [Fact]
    public void Unknown_own_count_yields_empty_rather_than_flooding()
    {
        Assert.Empty(TabRosterFilter.SelectOwn(RealDump(), ownCount: 0));
        Assert.Empty(TabRosterFilter.SelectOwn(null, ownCount: 27));
    }
}
