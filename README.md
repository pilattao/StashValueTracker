# Stash Value Tracker

An ExileCore2 plugin for **Path of Exile 2** that
remembers the contents and value of every stash tab you open and shows the combined worth of
your whole stash — across all tabs — in a single, sortable, filterable window.

Because the game only exposes the **currently open** tab to memory, the plugin snapshots each tab
as you open it and remembers those snapshots (on disk, per league). Open a tab again to refresh it.

## Features

- **Whole-stash value** aggregated into one window: one row per item (same item across tabs is
  summed), with quantity, unit price, total, and which tab(s) it's in.
- **Per-tab filter** — tick/untick tabs to include in the total; your selection is remembered.
- **Sortable, resizable table** — resize, reorder, hide and sort columns; layout persists.
- **Prices via NinjaPricer** — values are pulled from the loaded NinjaPricer plugin, shown in
  exalted with an approximate divine value, e.g. `1240 ex (~8.7 div)`.
- **Handles special "grid" stashes** (Currency, Runes/Socketable, Essence, Expedition, Abyss,
  Ritual, …) — reads their full contents, not just the visible page.
- **Per-league persistence** — snapshots are saved to disk and reload on startup, so totals are
  available before you re-open anything.
- **Convenience:** a toggle hotkey, and an optional "open/close the window automatically with the
  stash".

## Requirements

- ExileCore2 overlay (Windows).
- The **[NinjaPricer](https://github.com/zelekharibo/NinjaPricer)** plugin, loaded and with price
  data downloaded. This plugin does **not** price items itself — without NinjaPricer the window
  shows a "not loaded / waiting for price data" banner and records nothing.

## Install

1. Clone (or download) this repo into your ExileCore `Plugins/Source` folder so the path is:
   ```
   <ExileCore2>/Plugins/Source/StashValueTracker
   ```
   e.g. `git clone https://github.com/pilattao/StashValueTracker.git` inside `Plugins/Source`.
2. (Re)start ExileCore — it compiles the plugin automatically.
3. Make sure **NinjaPricer** is enabled and has prices.

> The `tests/` folder and the design docs are for development only; ExileCore ignores them.

## Usage

1. Enable the plugin and open the **Stash Value Tracker** window — tick **"Show value window"** in
   the plugin settings, press your bound hotkey, or enable **"Auto-open/close with stash"**.
2. Open your stash tabs once each so they get scanned. The open tab also re-scans when its item
   count changes, and switching tabs scans the new one.
3. The window shows the combined, sorted item list. Use the **left panel** to include/exclude
   tabs; **drag the vertical divider** to resize the panel; **Forget** drops a stale tab.

## Settings

- **Show value window** — open/close the window.
- **Auto-open/close with stash** — open the window when the stash opens, close it when it closes.
- **Toggle window hotkey** — bind a key to open/close the window in-game.
- **Scan debounce (ms)** — how long a tab must stay open before it's scanned.

Window state that persists across restarts: the tab include/exclude selection, the divider
position, and the table column layout (order, width, visibility, sort).

## Building (contributors)

Targets `net8.0-windows`; runs in-game on Windows. Pure-logic parts have xUnit tests that run on
any platform:

```bash
dotnet test tests/StashValueTracker.Tests/StashValueTracker.Tests.csproj
```

The plugin assembly can be compile-checked off-Windows with `EnableWindowsTargeting` and the
ExileCore2 reference DLLs pointed at via the `exileCore2Package` MSBuild property:

```bash
exileCore2Package="/path/to/exilecore/dlls" dotnet build StashValueTracker.csproj -c Debug
```

Those ExileCore2 DLLs are proprietary and are **not** committed (see `.gitignore`).

## License

Provided as-is for the PoE2 ExileCore community. Use at your own risk.
