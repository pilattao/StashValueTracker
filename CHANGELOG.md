# Changelog

All notable changes to **Stash Value Tracker** are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

_Nothing yet._

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

### 📣 Discord announcement

Copy-paste this into the app's Discord channel:

```
🎉 **Stash Value Tracker v1.1.0 is out!**

This update is all about cutting the noise and reading your stash at a glance.

**✨ New**
• **Value filters** — set a minimum **total** and/or **unit price** (in ex). Anything below drops out of the table, the grand total, *and* the per-tab totals. `0` = off, and your thresholds are remembered.
• **Item search** — quick name filter above the table.
• **Per-tab %** — each tab now shows its share of your selected total.
• **Both-currency tooltips** — hover any value to see it in divine *and* exalted (`53 div · 7 565 ex`).

**🔧 Improved**
• **Smart currency** — values auto-pick divine or exalted by size (goodbye `0.007 div`).
• **Thousands separators** — `50 000` instead of `50000`.
• **Cleaner table** — column dividers, aligned numbers, sort arrows.

📥 Update: pull the latest and restart ExileCore (it recompiles automatically).
Feedback and bug reports welcome! 🧡
```

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

[Unreleased]: https://github.com/pilattao/StashValueTracker/compare/v1.1.0...HEAD
[1.1.0]: https://github.com/pilattao/StashValueTracker/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/pilattao/StashValueTracker/releases/tag/v1.0.0
