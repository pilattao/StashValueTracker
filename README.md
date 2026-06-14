# Stash Value Tracker

An [ExileCore2](https://github.com/Stridemann/ExileCore) plugin for Path of Exile 2 that
remembers the contents and value of every stash tab you open, then shows the combined
worth of your whole stash across all tabs in a single, filterable summary.

## What it does

While enabled, the plugin passively records each stash tab you open — its items, their
values (priced via [NinjaPricer](https://github.com/zelekharibo/NinjaPricer)), the tab's
name/type, and when it was last scanned. Re-opening a tab re-scans it. A dedicated window
aggregates everything into a per-item summary, with filters for which tabs to include.

> Only the currently open tab is readable from game memory, so the plugin remembers
> snapshots of tabs as you open them. Re-open a tab to refresh its data.

## Status

v1 implemented and passing all automated tests; pending in-game verification on Windows.
The design spec is maintained locally (not committed).

## Requirements

- ExileCore2 overlay (Windows).
- **NinjaPricer** plugin loaded — this plugin uses its prices via PluginBridge and does not
  price items on its own. Without NinjaPricer, valuation is unavailable.

## Usage

1. Enable the plugin in ExileCore.
2. **NinjaPricer must be loaded** — this plugin prices items via its PluginBridge. Without it,
   valuation is unavailable and the window shows a "not loaded" banner.
3. Open stash tabs to scan them. The currently open tab also re-scans periodically (~2.5 s)
   and whenever its item count changes.
4. Toggle **"Show value window"** in the plugin settings to view the aggregated total across
   all remembered tabs.
5. Use the left panel checkboxes to include or exclude tabs from the total (all tabs are
   included by default; newly scanned tabs are auto-included). Click **"Forget"** to drop a
   stale tab and remove it from memory.
6. Snapshots persist to disk per league (in the plugin's `data/` folder) and reload
   automatically on startup — totals are available before you re-open any tabs.

### Hotkey

- In the plugin settings, bind a **toggle hotkey** and press it in-game to open or close the
  value window without going into settings.

### Tabs with sub-tabs (e.g. Rune or Affinity stash)

- Tabs that contain sub-tabs (e.g. Rune stash, Affinity stash) are tracked **per sub-tab**.
  Open each sub-tab at least once so the plugin can scan it.
- In the left-panel filter the sub-tabs appear **grouped under their parent tab**. The parent
  entry shows the **combined value** of all its sub-tabs and a **(N sub-tabs)** hint.
- The parent's **checkbox** toggles every sub-tab at once; **Forget** on the parent drops all
  sub-tabs from memory.

### Auto-refresh and re-scan interval

- **"Auto-refresh open tab"** (on by default) re-scans the currently open tab on a timer.
  Turn it off to scan only when you switch tabs (useful if you want no background activity).
- **"Re-scan interval"** controls the refresh cadence; lower the value for faster updates or
  raise it to reduce overhead.

### Summary table layout

- The summary table columns can be **resized**, **reordered**, and **hidden** by right-clicking
  a column header, and **sorted** by clicking a header. Numeric value columns are right-aligned.
  The layout (column order, widths, visibility, sort) persists across sessions.

## Building

Targets `net8.0-windows`; runs in-game on Windows. It can be compile-checked on Linux/CI
with `EnableWindowsTargeting` and the ExileCore2 reference DLLs supplied via the
`exileCore2Package` MSBuild property. Those DLLs are proprietary and are **not** committed
(see `.gitignore`).

## Manual test checklist (Windows / in-game)

- [ ] With NinjaPricer loaded and price data ready, open a Currency tab → items get scanned (log line appears).
- [ ] Toggle "Show value window" → window shows the tab in the left panel and items on the right.
- [ ] Open a second tab → it appears and is auto-included; totals update; an item in both tabs merges into one row with a "Tab +N" label and a tooltip listing both tabs.
- [ ] Add items to the open tab without closing it → within ~3s the tab re-scans and totals update.
- [ ] Uncheck a tab → its items/total drop out; re-check → they return; a newly scanned tab is included by default.
- [ ] "Forget" a tab → it disappears and the data file updates.
- [ ] Restart the overlay → snapshots reload from disk; totals present before re-opening tabs.
- [ ] Disable NinjaPricer → "not loaded" banner; no scanning.
- [ ] Switch league → window reflects the new league's separate snapshots; the old league's file is unchanged.
- [ ] Reorder tabs (if applicable) → confirm whether identity holds (name-based key); note any drift for the stable-id follow-up.
- [ ] Bind the toggle-window hotkey in settings; press it in-game → the window opens/closes.
- [ ] Open a nested stash (e.g. rune/affinity) and visit each sub-tab → each sub-tab is scanned; the filter shows them grouped under the parent with the parent's combined value and "(N sub-tabs)"; the parent checkbox toggles all sub-tabs and "Forget" on the parent drops them all.
- [ ] Switching sub-tabs does not overwrite other sub-tabs' values.
- [ ] Empty an active sub-tab and re-open/re-scan it → its stored value drops to 0 (not stuck at the old total); the parent's combined value decreases.
- [ ] Sub-tab entries are stable/distinct with no duplicates and no ghost "/ -1" row.
- [ ] Turn off "Auto-refresh open tab" → the open tab no longer re-scans on a timer; switching tabs still scans. Turn it on and change "Re-scan interval" → the open tab refreshes at the new cadence.
- [ ] In the summary table, resize/reorder/hide columns and click headers to sort; numeric columns are right-aligned and the layout persists across sessions.
