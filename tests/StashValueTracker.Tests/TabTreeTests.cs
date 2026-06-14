using System.Collections.Generic;
using System.Linq;
using StashValueTracker.Aggregation;
using StashValueTracker.Model;
using Xunit;

namespace StashValueTracker.Tests;

public class TabTreeTests
{
    private static TabSnapshot Tab(string key, string name, string? parent, double total) => new()
    {
        Key = key, Name = name, ParentName = parent,
        Items = new List<ItemSnapshot> { new() { DisplayName = "x", GroupKey = "x", StackSize = 1, TotalValueEx = total } },
    };

    [Fact]
    public void Ordinary_tabs_become_standalone_nodes()
    {
        var nodes = TabTree.Build(new[] { Tab("name:Currency", "Currency", null, 10) });
        var node = Assert.Single(nodes);
        Assert.False(node.IsGroup);
        Assert.Equal("Currency", node.Label);
        Assert.Equal(10, node.TotalEx);
        Assert.Single(node.Tabs);
    }

    [Fact]
    public void Sub_tabs_group_under_parent_and_sum()
    {
        var tabs = new[]
        {
            Tab("name:Affinity/sub:0", "Affinity / 0", "Affinity", 66.42),
            Tab("name:Affinity/sub:1", "Affinity / 1", "Affinity", 33.58),
        };
        var nodes = TabTree.Build(tabs);
        var group = Assert.Single(nodes);
        Assert.True(group.IsGroup);
        Assert.Equal("Affinity", group.Label);
        Assert.Equal(2, group.Tabs.Count);
        Assert.Equal(100, group.TotalEx);
    }

    [Fact]
    public void Mixed_ordinary_and_nested_sorted_by_label()
    {
        var tabs = new[]
        {
            Tab("name:Maps", "Maps", null, 5),
            Tab("name:Affinity/sub:0", "Affinity / 0", "Affinity", 1),
        };
        var nodes = TabTree.Build(tabs);
        Assert.Equal(2, nodes.Count);
        Assert.Equal("Affinity", nodes[0].Label);
        Assert.True(nodes[0].IsGroup);
        Assert.Equal("Maps", nodes[1].Label);
        Assert.False(nodes[1].IsGroup);
    }

    [Fact]
    public void Group_total_ignores_unpriced_items()
    {
        var tabs = new[]
        {
            Tab("name:A/sub:0", "A / 0", "A", 0),
            Tab("name:A/sub:1", "A / 1", "A", 7),
        };
        var group = Assert.Single(TabTree.Build(tabs));
        Assert.Equal(7, group.TotalEx);
    }

    [Fact]
    public void Null_input_is_safe()
    {
        Assert.Empty(TabTree.Build(null));
    }

    [Fact]
    public void Same_name_standalone_and_group_stay_separate_nodes()
    {
        var tabs = new[]
        {
            Tab("name:Affinity", "Affinity", null, 5),
            Tab("name:Affinity/sub:0", "Affinity / 0", "Affinity", 7),
        };
        var nodes = TabTree.Build(tabs);
        Assert.Equal(2, nodes.Count);
        Assert.All(nodes, n => Assert.Equal("Affinity", n.Label));
        Assert.Contains(nodes, n => !n.IsGroup);
        Assert.Contains(nodes, n => n.IsGroup);
    }
}
