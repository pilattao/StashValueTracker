using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using ImGuiNET;
using StashValueTracker;
using StashValueTracker.Aggregation;
using StashValueTracker.Formatting;
using StashValueTracker.Model;
using StashValueTracker.Storage;

namespace StashValueTracker.UI;

public sealed class ValueWindow
{
    private readonly Settings _settings;
    private readonly HashSet<string> _excluded;   // opt-out: everything not here is included (persisted via Settings)

    public ValueWindow(Settings settings)
    {
        _settings = settings;
        _excluded = new HashSet<string>(settings.ExcludedTabKeys ?? new List<string>());
    }

    // Toggle a tab's exclusion and mirror it into the persisted settings list.
    private void SetExcluded(string key, bool excluded)
    {
        if (excluded) _excluded.Add(key);
        else _excluded.Remove(key);
        _settings.ExcludedTabKeys = _excluded.ToList();
    }

    /// <summary>Renders the window. Sets <paramref name="showWindow"/> false when closed; calls
    /// <paramref name="requestSave"/> after any store mutation (e.g. Forget).</summary>
    public void Draw(SnapshotStore store, double divinePerExalted, bool bridgeAvailable, bool pricesReady,
                     Action requestSave, ref bool showWindow)
    {
        Theme.Push();
        try
        {
            if (!ImGui.Begin("Stash Value Tracker", ref showWindow)) { ImGui.End(); return; }

            if (!bridgeAvailable)
                ImGui.TextColored(new Vector4(1, 0.4f, 0.4f, 1), "NinjaPricer not loaded — valuation unavailable.");
            else if (!pricesReady)
                ImGui.TextColored(new Vector4(1, 0.85f, 0.3f, 1), "Waiting for price data...");

            var tabs = store.Tabs;
            var includeKeys = tabs.Select(t => t.Key).Where(k => !_excluded.Contains(k)).ToHashSet();
            var result = StashAggregator.Aggregate(tabs, includeKeys);

            ImGui.TextColored(new Vector4(0.55f, 0.85f, 1f, 1f),
                $"Total (selected): {CurrencyFormat.ExWithDiv(result.GrandTotalEx, divinePerExalted)}");
            ImGui.SameLine();
            ImGui.TextDisabled($"   |   {result.UnpricedCount} items unpriced");
            ImGui.Separator();

            DrawTabFilterPanel(store, tabs, divinePerExalted, requestSave, _settings.TabPanelWidth.Value);
            ImGui.SameLine();
            DrawSplitter();
            ImGui.SameLine();
            DrawSummaryTable(result, divinePerExalted);

            ImGui.End();
        }
        finally
        {
            Theme.Pop();
        }
    }

    // A thin draggable bar between the tab panel and the summary table; persists width via Settings.
    private void DrawSplitter()
    {
        const float thickness = 6f;
        var height = ImGui.GetContentRegionAvail().Y;

        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(1, 1, 1, 0.06f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(1, 1, 1, 0.18f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.40f, 0.70f, 0.95f, 0.5f));
        ImGui.Button("##vsplit", new Vector2(thickness, height));
        ImGui.PopStyleColor(3);

        if (ImGui.IsItemHovered() || ImGui.IsItemActive())
            ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeEW);
        if (ImGui.IsItemActive())
            _settings.TabPanelWidth.Value = Math.Clamp(_settings.TabPanelWidth.Value + ImGui.GetIO().MouseDelta.X, 140f, 600f);
    }

    private void DrawTabFilterPanel(SnapshotStore store, IReadOnlyList<TabSnapshot> tabs, double divinePerExalted, Action requestSave, float panelWidth)
    {
        ImGui.BeginChild("tabs", new Vector2(panelWidth, 0), ImGuiChildFlags.Border);
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
                SetExcluded(t.Key, !parentChecked);
        }
        ImGui.SameLine();

        var open = ImGui.TreeNodeEx($"{node.Label} ({node.Tabs.Count} sub-tabs)##grp");
        ImGui.SameLine();
        ImGui.TextDisabled($"  {CurrencyFormat.ExWithDiv(node.TotalEx, divinePerExalted)}");
        if (RightSmallButton("Forget##grp"))
        {
            foreach (var t in node.Tabs)
            {
                store.ForgetTab(t.Key);
                SetExcluded(t.Key, false);
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
            SetExcluded(tab.Key, !included);

        var tabTotal = tab.Items.Where(i => i.TotalValueEx > 0).Sum(i => i.TotalValueEx);
        ImGui.TextDisabled($"  {CurrencyFormat.ExWithDiv(tabTotal, divinePerExalted)} · {Ago(tab.LastScannedUtc)}");

        if (RightSmallButton("Forget"))
        {
            store.ForgetTab(tab.Key);
            SetExcluded(tab.Key, false);
            requestSave();
        }

        if (indent) ImGui.Unindent();
        ImGui.PopID();
    }

    private static void DrawSummaryTable(AggregationResult result, double divinePerExalted)
    {
        ImGui.BeginChild("summary", new Vector2(0, 0), ImGuiChildFlags.Border);

        // ScrollX + all-fixed columns: dragging a column border resizes only that column (the table
        // scrolls / grows) instead of squashing its neighbour.
        var flags = ImGuiTableFlags.Resizable | ImGuiTableFlags.Reorderable | ImGuiTableFlags.Hideable
                  | ImGuiTableFlags.Sortable | ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerH
                  | ImGuiTableFlags.ScrollX | ImGuiTableFlags.ScrollY;
        if (ImGui.BeginTable("svt_items_v2", 5, flags))
        {
            ImGui.TableSetupColumn("Item", ImGuiTableColumnFlags.WidthFixed, 320);
            ImGui.TableSetupColumn("Tab", ImGuiTableColumnFlags.WidthFixed, 120);
            ImGui.TableSetupColumn("Qty", ImGuiTableColumnFlags.WidthFixed, 60);
            ImGui.TableSetupColumn("Unit", ImGuiTableColumnFlags.WidthFixed, 130);
            ImGui.TableSetupColumn("Total", ImGuiTableColumnFlags.WidthFixed | ImGuiTableColumnFlags.DefaultSort, 150);
            ImGui.TableHeadersRow();

            foreach (var row in SortRows(result.Rows))
            {
                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                ImGui.TextUnformatted(row.DisplayName);

                ImGui.TableNextColumn();
                ImGui.TextUnformatted(row.TabLabel);
                if (row.TabNames.Count > 1 && ImGui.IsItemHovered())
                    ImGui.SetTooltip(string.Join("\n", row.TabNames));

                ImGui.TableNextColumn(); RightText(row.Quantity.ToString());

                ImGui.TableNextColumn(); RightText(CurrencyFormat.ExWithDiv(row.UnitEx, divinePerExalted));

                ImGui.TableNextColumn(); RightText(CurrencyFormat.ExWithDiv(row.TotalEx, divinePerExalted));
            }

            ImGui.EndTable();
        }

        ImGui.EndChild();
    }

    // Draws a SmallButton flush against the right edge of the current content region, so buttons on
    // consecutive rows line up vertically regardless of the text before them.
    private static bool RightSmallButton(string label)
    {
        var visible = label;
        var hash = label.IndexOf("##", StringComparison.Ordinal);
        if (hash >= 0) visible = label.Substring(0, hash);
        var w = ImGui.CalcTextSize(visible).X + ImGui.GetStyle().FramePadding.X * 2f;

        ImGui.SameLine();
        var spare = ImGui.GetContentRegionAvail().X - w;
        if (spare > 0) ImGui.SetCursorPosX(ImGui.GetCursorPosX() + spare);
        return ImGui.SmallButton(label);
    }

    private static void RightText(string s)
    {
        var w = ImGui.CalcTextSize(s).X;
        var avail = ImGui.GetContentRegionAvail().X;
        var off = avail - w;
        if (off > 0) ImGui.SetCursorPosX(ImGui.GetCursorPosX() + off);
        ImGui.TextUnformatted(s);
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
