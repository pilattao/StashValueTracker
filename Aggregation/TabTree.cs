using System;
using System.Collections.Generic;
using System.Linq;
using StashValueTracker.Model;

namespace StashValueTracker.Aggregation;

/// <summary>A node in the tab-filter tree: a standalone tab, or a parent grouping its sub-tabs.</summary>
public sealed class TabNode
{
    public bool IsGroup { get; init; }
    public string Label { get; init; } = "";
    public string? ParentName { get; init; }
    public IReadOnlyList<TabSnapshot> Tabs { get; init; } = Array.Empty<TabSnapshot>();
    public double TotalEx { get; init; }
}

public static class TabTree
{
    public static IReadOnlyList<TabNode> Build(IEnumerable<TabSnapshot>? tabs)
    {
        var list = (tabs ?? Enumerable.Empty<TabSnapshot>()).Where(t => t != null).ToList();
        var nodes = new List<TabNode>();

        foreach (var t in list.Where(t => string.IsNullOrEmpty(t.ParentName)))
        {
            nodes.Add(new TabNode
            {
                IsGroup = false,
                Label = t.Name,
                ParentName = null,
                Tabs = new[] { t },
                TotalEx = PricedTotal(t),
            });
        }

        foreach (var group in list.Where(t => !string.IsNullOrEmpty(t.ParentName))
                                   .GroupBy(t => t.ParentName!))
        {
            var children = group.OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase).ToList();
            nodes.Add(new TabNode
            {
                IsGroup = true,
                Label = group.Key,
                ParentName = group.Key,
                Tabs = children,
                TotalEx = children.Sum(PricedTotal),
            });
        }

        return nodes.OrderBy(n => n.Label, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static double PricedTotal(TabSnapshot t) =>
        (t.Items ?? new List<ItemSnapshot>()).Where(i => i.TotalValueEx > 0).Sum(i => i.TotalValueEx);
}
