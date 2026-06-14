using System.Collections.Generic;
using StashValueTracker.Aggregation;
using StashValueTracker.Model;
using Xunit;

namespace StashValueTracker.Tests;

public class StashAggregatorTests
{
    private static TabSnapshot Tab(string key, string name, params ItemSnapshot[] items) =>
        new() { Key = key, Name = name, Items = new List<ItemSnapshot>(items) };

    private static ItemSnapshot Item(string name, int stack, double totalEx) =>
        new() { DisplayName = name, GroupKey = name, StackSize = stack, TotalValueEx = totalEx };

    [Fact]
    public void Merges_same_item_across_tabs_and_sums_qty_and_total()
    {
        var tabs = new[]
        {
            Tab("name:Currency", "Currency", Item("Exalted Orb", 100, 100)),
            Tab("name:Frags", "Frags", Item("Exalted Orb", 40, 40)),
        };

        var result = StashAggregator.Aggregate(tabs, new HashSet<string> { "name:Currency", "name:Frags" });

        var row = Assert.Single(result.Rows);
        Assert.Equal("Exalted Orb", row.DisplayName);
        Assert.Equal(140, row.Quantity);
        Assert.Equal(140, row.TotalEx);
        Assert.Equal(1, row.UnitEx);
        Assert.Equal(new[] { "Currency", "Frags" }, row.TabNames);
        Assert.Equal("Currency +1", row.TabLabel);
    }

    [Fact]
    public void Single_tab_item_shows_plain_tab_label()
    {
        var tabs = new[] { Tab("name:Uniques", "Uniques", Item("Headhunter", 1, 900)) };
        var result = StashAggregator.Aggregate(tabs, new HashSet<string> { "name:Uniques" });
        var row = Assert.Single(result.Rows);
        Assert.Equal("Uniques", row.TabLabel);
        Assert.Equal(900, row.UnitEx);
    }

    [Fact]
    public void Excludes_and_counts_unpriced_items()
    {
        var tabs = new[]
        {
            Tab("name:Currency", "Currency", Item("Exalted Orb", 10, 10), Item("Rare Ring", 1, 0)),
        };
        var result = StashAggregator.Aggregate(tabs, new HashSet<string> { "name:Currency" });
        Assert.Single(result.Rows);
        Assert.Equal(1, result.UnpricedCount);
        Assert.Equal(10, result.GrandTotalEx);
    }

    [Fact]
    public void Respects_include_filter()
    {
        var tabs = new[]
        {
            Tab("name:Currency", "Currency", Item("Exalted Orb", 10, 10)),
            Tab("name:Maps", "Maps", Item("Divine Orb", 5, 1000)),
        };
        var result = StashAggregator.Aggregate(tabs, new HashSet<string> { "name:Currency" });
        var row = Assert.Single(result.Rows);
        Assert.Equal("Exalted Orb", row.DisplayName);
        Assert.Equal(10, result.GrandTotalEx);
    }

    [Fact]
    public void Empty_filter_yields_no_rows()
    {
        var tabs = new[] { Tab("name:Currency", "Currency", Item("Exalted Orb", 10, 10)) };
        var result = StashAggregator.Aggregate(tabs, new HashSet<string>());
        Assert.Empty(result.Rows);
        Assert.Equal(0, result.GrandTotalEx);
    }

    [Fact]
    public void Rows_sorted_by_total_descending()
    {
        var tabs = new[] { Tab("name:A", "A", Item("Cheap", 1, 5), Item("Pricey", 1, 500)) };
        var result = StashAggregator.Aggregate(tabs, new HashSet<string> { "name:A" });
        Assert.Equal("Pricey", result.Rows[0].DisplayName);
        Assert.Equal("Cheap", result.Rows[1].DisplayName);
    }

    [Fact]
    public void Null_inputs_are_safe()
    {
        var result = StashAggregator.Aggregate(null, null);
        Assert.Empty(result.Rows);
        Assert.Equal(0, result.GrandTotalEx);
        Assert.Equal(0, result.UnpricedCount);
    }
}
