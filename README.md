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

🚧 In development. The design spec lives in
[`docs/superpowers/specs/`](docs/superpowers/specs/2026-06-14-stash-value-tracker-design.md).

## Requirements

- ExileCore2 overlay (Windows).
- **NinjaPricer** plugin loaded — this plugin uses its prices via PluginBridge and does not
  price items on its own. Without NinjaPricer, valuation is unavailable.

## Building

Targets `net8.0-windows`; runs in-game on Windows. It can be compile-checked on Linux/CI
with `EnableWindowsTargeting` and the ExileCore2 reference DLLs supplied via the
`exileCore2Package` MSBuild property. Those DLLs are proprietary and are **not** committed
(see `.gitignore`).
