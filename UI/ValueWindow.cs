using System;
using System.Collections.Generic;
using System.Linq;
using ImGuiNET;
using StashValueTracker.Aggregation;
using StashValueTracker.Formatting;
using StashValueTracker.Model;
using StashValueTracker.Storage;

namespace StashValueTracker.UI;

public sealed class ValueWindow
{
    private readonly HashSet<string> _excluded = new();   // opt-out: everything not here is included

    /// <summary>Clears the tab-exclusion filter (call on league change so a new league starts fresh).</summary>
    public void ResetExclusions() => _excluded.Clear();

    /// <summary>Renders the window. Sets <paramref name="showWindow"/> false when closed; calls
    /// <paramref name="requestSave"/> after any store mutation (e.g. Forget).</summary>
    public void Draw(SnapshotStore store, double divinePerExalted, bool bridgeAvailable, bool pricesReady,
                     Action requestSave, ref bool showWindow)
    {
        if (!ImGui.Begin("Stash Value Tracker", ref showWindow))
        {
            ImGui.End();
            return;
        }

        if (!bridgeAvailable)
            ImGui.TextColored(new System.Numerics.Vector4(1, 0.4f, 0.4f, 1), "NinjaPricer not loaded — valuation unavailable.");
        else if (!pricesReady)
            ImGui.TextColored(new System.Numerics.Vector4(1, 0.85f, 0.3f, 1), "Waiting for price data...");

        var tabs = store.Tabs;
        var includeKeys = tabs.Select(t => t.Key).Where(k => !_excluded.Contains(k)).ToHashSet();
        var result = StashAggregator.Aggregate(tabs, includeKeys);

        ImGui.Text($"Total (selected): {CurrencyFormat.ExWithDiv(result.GrandTotalEx, divinePerExalted)}");
        ImGui.SameLine();
        ImGui.TextDisabled($"   |   {result.UnpricedCount} items unpriced");
        ImGui.Separator();

        DrawTabFilterPanel(store, tabs, divinePerExalted, requestSave);
        ImGui.SameLine();
        DrawSummaryTable(result, divinePerExalted);

        ImGui.End();
    }

    private void DrawTabFilterPanel(SnapshotStore store, IReadOnlyList<TabSnapshot> tabs, double divinePerExalted, Action requestSave)
    {
        ImGui.BeginChild("tabs", new System.Numerics.Vector2(240, 0), ImGuiChildFlags.Border);
        ImGui.TextDisabled("Tabs");
        ImGui.Separator();

        foreach (var node in TabTree.Build(tabs))
        {
            if (node.IsGroup)
                DrawGroupNode(store, node, divinePerExalted, requestSave);
            else
                DrawTabRow(store, node.Tabs[0], divinePerExalted, requestSave, indent: false);
        }

        ImGui.EndChild();
    }

    private void DrawGroupNode(SnapshotStore store, TabNode node, double divinePerExalted, Action requestSave)
    {
        ImGui.PushID("grp:" + node.Label);

        var allIncluded = node.Tabs.All(t => !_excluded.Contains(t.Key));
        var parentChecked = allIncluded;
        if (ImGui.Checkbox("##grpsel", ref parentChecked))
        {
            foreach (var t in node.Tabs)
            {
                if (parentChecked) _excluded.Remove(t.Key);
                else _excluded.Add(t.Key);
            }
        }
        ImGui.SameLine();

        var open = ImGui.TreeNodeEx($"{node.Label} ({node.Tabs.Count} sub-tabs)##grp");
        ImGui.SameLine();
        ImGui.TextDisabled($"  {CurrencyFormat.ExWithDiv(node.TotalEx, divinePerExalted)}");
        ImGui.SameLine();
        if (ImGui.SmallButton("Forget##grp"))
        {
            foreach (var t in node.Tabs)
            {
                store.ForgetTab(t.Key);
                _excluded.Remove(t.Key);
            }
            requestSave();
        }
        if (open)
        {
            foreach (var t in node.Tabs)
                DrawTabRow(store, t, divinePerExalted, requestSave, indent: true);
            ImGui.TreePop();
        }

        ImGui.PopID();
    }

    private void DrawTabRow(SnapshotStore store, TabSnapshot tab, double divinePerExalted, Action requestSave, bool indent)
    {
        ImGui.PushID(tab.Key);
        if (indent) ImGui.Indent();

        var included = !_excluded.Contains(tab.Key);
        if (ImGui.Checkbox($"{tab.Name}##sel", ref included))
        {
            if (included) _excluded.Remove(tab.Key);
            else _excluded.Add(tab.Key);
        }

        var tabTotal = tab.Items.Where(i => i.TotalValueEx > 0).Sum(i => i.TotalValueEx);
        ImGui.TextDisabled($"  {CurrencyFormat.ExWithDiv(tabTotal, divinePerExalted)} · {Ago(tab.LastScannedUtc)}");

        ImGui.SameLine();
        if (ImGui.SmallButton("Forget"))
        {
            store.ForgetTab(tab.Key);
            _excluded.Remove(tab.Key);
            requestSave();
        }

        if (indent) ImGui.Unindent();
        ImGui.PopID();
    }

    private static void DrawSummaryTable(AggregationResult result, double divinePerExalted)
    {
        ImGui.BeginChild("summary", new System.Numerics.Vector2(0, 0), ImGuiChildFlags.Border);

        var flags = ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.RowBg | ImGuiTableFlags.Sortable | ImGuiTableFlags.ScrollY;
        if (ImGui.BeginTable("svt_items", 5, flags))
        {
            ImGui.TableSetupColumn("Item", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Tab", ImGuiTableColumnFlags.WidthFixed, 120);
            ImGui.TableSetupColumn("Qty", ImGuiTableColumnFlags.WidthFixed, 60);
            ImGui.TableSetupColumn("Unit", ImGuiTableColumnFlags.WidthFixed, 130);
            ImGui.TableSetupColumn("Total", ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.DefaultSort, 150);
            ImGui.TableHeadersRow();

            foreach (var row in SortRows(result.Rows))
            {
                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                ImGui.Text(row.DisplayName);

                ImGui.TableNextColumn();
                ImGui.Text(row.TabLabel);
                if (row.TabNames.Count > 1 && ImGui.IsItemHovered())
                    ImGui.SetTooltip(string.Join("\n", row.TabNames));

                ImGui.TableNextColumn();
                ImGui.Text(row.Quantity.ToString());

                ImGui.TableNextColumn();
                ImGui.Text(CurrencyFormat.ExWithDiv(row.UnitEx, divinePerExalted));

                ImGui.TableNextColumn();
                ImGui.Text(CurrencyFormat.ExWithDiv(row.TotalEx, divinePerExalted));
            }

            ImGui.EndTable();
        }

        ImGui.EndChild();
    }

    // Applies ImGui's current sort spec; defaults to Total desc (already provided by the aggregator).
    private static List<AggregatedRow> SortRows(IReadOnlyList<AggregatedRow> rows)
    {
        var list = rows.ToList();
        var specs = ImGui.TableGetSortSpecs();
        if (specs.SpecsCount == 0) return list;

        var spec = specs.Specs;
        var ascending = spec.SortDirection == ImGuiSortDirection.Ascending;
        Comparison<AggregatedRow> cmp = spec.ColumnIndex switch
        {
            0 => (a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase),
            1 => (a, b) => string.Compare(a.TabLabel, b.TabLabel, StringComparison.OrdinalIgnoreCase),
            2 => (a, b) => a.Quantity.CompareTo(b.Quantity),
            3 => (a, b) => a.UnitEx.CompareTo(b.UnitEx),
            _ => (a, b) => a.TotalEx.CompareTo(b.TotalEx),
        };
        list.Sort((a, b) => ascending ? cmp(a, b) : -cmp(a, b));
        return list;
    }

    private static string Ago(DateTime utc)
    {
        if (utc == default) return "not scanned";
        var span = DateTime.UtcNow - utc;
        if (span.TotalMinutes < 1) return "just now";
        if (span.TotalMinutes < 60) return $"{(int)span.TotalMinutes}m ago";
        if (span.TotalHours < 24) return $"{(int)span.TotalHours}h ago";
        return $"{(int)span.TotalDays}d ago";
    }
}
