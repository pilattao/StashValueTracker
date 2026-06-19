using System.Collections.Generic;
using System.Linq;
using StashValueTracker.Model;
using StashValueTracker.Tabs;
using Xunit;

namespace StashValueTracker.Tests;

public class TabReconcilerMatchTests
{
    private static TabRosterEntry Live(string name, int color = 0, string type = "Normal", int idx = 0) =>
        new() { Name = name, ColorArgb = color, TabType = type, VisibleIndex = idx };

    private static TabSnapshot Stored(string key, string name, int color = 0, string type = "Normal", int idx = 0) =>
        new() { Key = key, Name = name, ColorArgb = color, TabType = type, VisibleIndex = idx, Scanned = true };

    [Fact]
    public void Matches_by_exact_name()
    {
        var roster = new[] { Live("Currency", color: 5, idx: 2) };
        var stored = new[] { Stored("k1", "Currency", color: 9, idx: 7) };
        var outcome = TabReconciler.Match(roster, stored);
        var m = Assert.Single(outcome.Matches);
        Assert.Equal(TabMatchKind.Name, m.Kind);
        Assert.Same(stored[0], m.Stored);
        Assert.Empty(outcome.Missing);
    }

    [Fact]
    public void Pure_rename_matches_by_signature()
    {
        // name changed; type+color+index unchanged → unique signature candidate
        var roster = new[] { Live("Loot", color: 5, type: "Normal", idx: 3) };
        var stored = new[] { Stored("k1", "OldName", color: 5, type: "Normal", idx: 3) };
        var outcome = TabReconciler.Match(roster, stored);
        var m = Assert.Single(outcome.Matches);
        Assert.Equal(TabMatchKind.Signature, m.Kind);
        Assert.Same(stored[0], m.Stored);
        Assert.Empty(outcome.Missing);
    }

    [Fact]
    public void Ambiguous_signature_is_not_guessed()
    {
        // two orphans of same type; live matches color of one and index of the other → ambiguous → New
        var roster = new[] { Live("New", color: 5, type: "Normal", idx: 9) };
        var stored = new[]
        {
            Stored("k1", "A", color: 5, type: "Normal", idx: 1),
            Stored("k2", "B", color: 0, type: "Normal", idx: 9),
        };
        var outcome = TabReconciler.Match(roster, stored);
        Assert.Equal(TabMatchKind.New, outcome.Matches[0].Kind);
        Assert.Null(outcome.Matches[0].Stored);
        Assert.Equal(2, outcome.Missing.Count);
    }

    [Fact]
    public void Brand_new_tab_has_no_stored_and_no_missing()
    {
        var outcome = TabReconciler.Match(new[] { Live("Fresh") }, new TabSnapshot[0]);
        Assert.Equal(TabMatchKind.New, Assert.Single(outcome.Matches).Kind);
        Assert.Empty(outcome.Missing);
    }

    [Fact]
    public void Deleted_tab_is_reported_missing()
    {
        var outcome = TabReconciler.Match(new TabRosterEntry[0], new[] { Stored("k1", "Gone") });
        Assert.Empty(outcome.Matches);
        Assert.Equal("k1", Assert.Single(outcome.Missing).Key);
    }

    [Fact]
    public void Name_match_wins_before_signature_claims_orphan()
    {
        // exact-name stored must be claimed in tier 1, leaving the other as signature/none
        var roster = new[] { Live("Currency", color: 5, type: "Normal", idx: 0) };
        var stored = new[]
        {
            Stored("k1", "Currency", color: 1, type: "Normal", idx: 0),
            Stored("k2", "Other",    color: 5, type: "Normal", idx: 0),
        };
        var outcome = TabReconciler.Match(roster, stored);
        Assert.Equal(TabMatchKind.Name, outcome.Matches[0].Kind);
        Assert.Same(stored[0], outcome.Matches[0].Stored);
        Assert.Same(stored[1], Assert.Single(outcome.Missing));
    }
}
