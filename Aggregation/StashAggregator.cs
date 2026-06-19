using System;
using System.Collections.Generic;
using System.Linq;
using StashValueTracker.Model;

namespace StashValueTracker.Aggregation;

public sealed class AggregatedRow
{
    public string DisplayName { get; init; } = "";
    public string GroupKey { get; init; } = "";
    public int Quantity { get; init; }
    public double TotalEx { get; init; }
    public double UnitEx => Quantity > 0 ? TotalEx / Quantity : 0;
    public IReadOnlyList<string> TabNames { get; init; } = Array.Empty<string>();

    public string TabLabel =>
        TabNames.Count == 0 ? "" :
        TabNames.Count == 1 ? TabNames[0] :
        $"{TabNames[0]} +{TabNames.Count - 1}";
}

public sealed class AggregationResult
{
    public IReadOnlyList<AggregatedRow> Rows { get; init; } = Array.Empty<AggregatedRow>();
    public double GrandTotalEx { get; init; }
    public int UnpricedCount { get; init; }
    public int HiddenCount { get; init; }
    public IReadOnlyDictionary<string, double> TabTotalsEx { get; init; } = new Dictionary<string, double>();
}

public static class StashAggregator
{
    public static AggregationResult Aggregate(IEnumerable<TabSnapshot> tabs, ISet<string> includeKeys,
                                              double minTotalEx = 0, double minUnitEx = 0)
    {
        var included = (tabs ?? Enumerable.Empty<TabSnapshot>())
            .Where(t => t != null && includeKeys != null && includeKeys.Contains(t.Key))
            .OrderBy(t => t.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(t => t.Key, StringComparer.Ordinal)
            .ToList();

        var unpriced = 0;
        var groups = new Dictionary<string, GroupAccum>();

        foreach (var tab in included)
        {
            foreach (var item in tab.Items ?? new List<ItemSnapshot>())
            {
                if (item.TotalValueEx <= 0) { unpriced++; continue; }

                if (!groups.TryGetValue(item.GroupKey, out var acc))
                {
                    acc = new GroupAccum { DisplayName = item.DisplayName, GroupKey = item.GroupKey };
                    groups[item.GroupKey] = acc;
                }

                acc.Quantity += item.StackSize;
                acc.TotalEx += item.TotalValueEx;
                if (!acc.TabNames.Contains(tab.Name)) acc.TabNames.Add(tab.Name);
                acc.PerTab[tab.Key] = acc.PerTab.GetValueOrDefault(tab.Key) + item.TotalValueEx;
            }
        }

        var hidden = 0;
        var kept = new List<GroupAccum>();
        var tabTotals = new Dictionary<string, double>();
        foreach (var g in groups.Values)
        {
            var unit = g.Quantity > 0 ? g.TotalEx / g.Quantity : 0;
            var passTotal = minTotalEx <= 0 || g.TotalEx >= minTotalEx;
            var passUnit = minUnitEx <= 0 || unit >= minUnitEx;
            if (passTotal && passUnit)
            {
                kept.Add(g);
                foreach (var kv in g.PerTab)
                    tabTotals[kv.Key] = tabTotals.GetValueOrDefault(kv.Key) + kv.Value;
            }
            else hidden++;
        }

        var rows = kept
            .Select(g => new AggregatedRow
            {
                DisplayName = g.DisplayName,
                GroupKey = g.GroupKey,
                Quantity = g.Quantity,
                TotalEx = g.TotalEx,
                TabNames = g.TabNames.AsReadOnly(),
            })
            .OrderByDescending(r => r.TotalEx)
            .ToList();

        return new AggregationResult
        {
            Rows = rows,
            GrandTotalEx = rows.Sum(r => r.TotalEx),
            UnpricedCount = unpriced,
            HiddenCount = hidden,
            TabTotalsEx = tabTotals,
        };
    }

    private sealed class GroupAccum
    {
        public string DisplayName = "";
        public string GroupKey = "";
        public int Quantity;
        public double TotalEx;
        public readonly List<string> TabNames = new();
        public readonly Dictionary<string, double> PerTab = new();   // tab.Key -> contribution
    }
}
