# Changelog

All notable changes to **Stash Value Tracker** are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

_Nothing yet._

## [1.2.0] - 2026-06-19

This release makes the tab panel mirror your real stash: every tab, in its own
colour, with values that follow a tab when you rename, move, or recolour it.

### Added
- **Coloured tab frames** — each tab in the left panel is framed in its in-game
  tab colour, read from the live stash.
- **All your tabs, always** — the panel now lists every stash tab (from the live
  tab roster), including ones you have not opened yet. Unscanned tabs appear
  greyed as `not scanned`, carry no value, and are not counted until you open
  them once.
- **Tab values survive edits** — a tab's stored value now follows it when you
  **rename, move, or recolour** it in-game, instead of orphaning the old data. It
  is matched first by name, then by colour/type/position, then by tab contents.
- **Divine rate in the header** — the current divine↔exalted rate is shown as
  `1 div = N ex`.

### Changed
- **Tabs ordered by stash position** — the left panel now follows your in-game
  tab order instead of sorting alphabetically.
- **Forget now resets a tab** — Forget clears a tab's cached value and returns it
  to `not scanned` rather than removing the row; the tab still exists in your
  stash, so it stays listed and re-fills the next time you open it.

### Fixed
- **Per-tab contribution %** now renders — the `%` sign was being swallowed by
  ImGui's text formatting.
- **Tab-name tooltips** no longer break when a tab name contains a `%` character.

## [1.1.0] - 2026-06-19

This release is about cutting the noise and reading your stash at a glance:
value filters, smarter currency display, and a more legible table.

### Added
- **Value filters** — set a **minimum total** and/or **minimum unit price** (in
  exalted) in the window. Items below either threshold are removed from the
  table, the grand total, **and** the per-tab totals. `0` disables a threshold;
  the thresholds are remembered between sessions.
- **"hidden by filter" counter** — shows how many items the filters are currently
  hiding (only when a filter is active).
- **Item search** — a search box above the table filters rows by name. It is
  view-only: it never changes any total.
- **Per-tab contribution %** — each included tab in the left panel now shows its
  share of the selected total (e.g. `18.2 div · 60% · 2m ago`).
- **Both-currency tooltips** — hover any value (header total, table cells, tab
  totals) to see it in both divine and exalted, e.g. `53 div · 7 565 ex`.

### Changed
- **Auto-unit currency** — values now display in the most readable unit: divine
  when the value is at least 1 divine, otherwise exalted. No more `0.007 div`.
- **Thousands separators** — large numbers are grouped for readability
  (`50 000`, `1 240`).
- **More legible table** — vertical column dividers, left-aligned numbers that
  line up under their headers, and visible sort arrows.

### Notes
- Excluded (unticked) tabs show their raw, unfiltered total and no percentage —
  the filters apply to what's counted in the selected total.
- Display is exalted/divine only; there is no chaos unit and no currency selector
  — the auto-unit handles the choice for you.

## [1.0.0] - 2026-06-15

First release.

### Added
- **Whole-stash value** in one window: one row per item (the same item across
  tabs is summed), with quantity, unit price, total, and which tab(s) it's in.
- **Per-tab filter** — tick/untick tabs to include in the total; the selection
  is remembered.
- **Sortable, resizable table** — resize, reorder, hide, and sort columns; the
  layout persists. A draggable splitter between the tab panel and the table, with
  a persisted width.
- **Prices via NinjaPricer** — values are read from the loaded NinjaPricer
  plugin, shown in exalted with an approximate divine value.
- **"Grid" stash support** — reads the full contents of Currency, Runes/
  Socketable, Essence, Expedition, Abyss, Ritual and similar tabs, not just the
  visible page.
- **Per-league persistence** — snapshots are saved to disk per league and reload
  on startup, so totals are available before re-opening any tab.
- **Convenience** — a toggle hotkey, and an optional "open/close the window
  automatically with the stash".

[Unreleased]: https://github.com/pilattao/StashValueTracker/compare/v1.2.0...HEAD
[1.2.0]: https://github.com/pilattao/StashValueTracker/compare/v1.1.0...v1.2.0
[1.1.0]: https://github.com/pilattao/StashValueTracker/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/pilattao/StashValueTracker/releases/tag/v1.0.0
