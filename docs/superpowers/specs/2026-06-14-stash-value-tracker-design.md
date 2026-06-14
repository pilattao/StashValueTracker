# Stash Value Tracker — Design Spec

**Status:** Approved design (brainstorming complete)
**Date:** 2026-06-14
**Type:** New ExileCore2 plugin (PoE2 overlay)

## Goal

A plugin that, while enabled, passively remembers the contents and value of every
stash tab the user opens, and presents a single aggregated, filterable view of the
total wealth across all remembered tabs — broken down per item line.

## User scenario

While the plugin is enabled, the user opens the stash on any tab. The plugin records
which items are there and what they are worth (priced exactly like NinjaPricer), plus
the tab itself: its name, type, and when it was last scanned. Re-opening a tab
re-scans it. A dedicated window shows the combined value of the whole stash across all
tabs, as a summary list by item, with filters for which tabs to include.

## Key decisions (from brainstorming)

1. **Pricing source: NinjaPricer only, via PluginBridge.** No own price source / fallback.
   If NinjaPricer is not loaded → the window shows a warning and no valuation happens.
2. **Persistence: yes, to disk (JSON).** Snapshots load on startup; per-league files.
   The window shows "last scanned N ago" so staleness is visible.
3. **Value is a snapshot captured at scan time.** Re-opening a tab re-scans and refreshes
   it. Rationale: `NinjaPrice.GetValue` needs a live `Entity`; once a tab is closed its
   entities are gone, so unique/rare gear cannot be re-priced. Snapshot-at-scan matches
   the stated "re-scan on re-open" behaviour and keeps the model consistent.
4. **Main view: aggregated item list.** Identical items across tabs sum into one row
   (name, source tab(s), total quantity, unit price, total). Sortable; default sort by total.
5. **Currency: exalted, with `(~Y div)` shown on every value** (not only large ones).
   The bridge returns a raw chaos-equivalent number. Following RitualHelper's proven
   approach, normalize to exalted by dividing by the raw value of an **Exalted Orb**
   (`GetBaseItemTypeValue`), and derive the divine display ratio from a **Divine Orb**:
   `exalted(item) = raw(item) / raw(ExaltedOrb)`,
   `divinePerExalted = raw(ExaltedOrb) / raw(DivineOrb)`.
6. **Unpriced items (value 0) are hidden** from the list and excluded from totals; a
   footer shows the count of unpriced items so coverage is visible.
7. **Tab column merges across tabs:** one row per item; the "Tab" column shows the single
   tab name, or `Currency +2` with a hover tooltip listing all tabs when the item spans
   several.

## Constraints (verified against the codebase)

- **Only the currently open/visible tab is readable.** `StashElement.VisibleStash.VisibleInventoryItems`
  returns items of the open tab only; closed tabs' contents are not in memory. This is the
  reason the plugin must remember snapshots.
- **Tab metadata is available:** `StashElement.VisibleStashIndex` / `IndexVisibleStash`
  (current tab index), `TabName` (current tab name), `VisibleStash.InvType` (tab type),
  and `GetStashTabs` / `AllInventories` / `GetStashName` (all tabs and names).
  Confirmed present in `ExileCore2.dll`.
- **Bridge API:** `GameController.PluginBridge.GetMethod<Func<Entity,double>>("NinjaPrice.GetValue")`
  returns the per-item value (already accounts for stack size); units are exalted-equivalent
  (legacy `MinChaosValue` naming). Also `NinjaPrice.GetBaseItemTypeValue(BaseItemType)→double`.
  Both return `0` for items NinjaPricer cannot price.
- **Item access:** each `NormalInventoryItem` exposes `.Item` (the `Entity`), grid pos/size,
  and components `Stack` (size), `Mods` (rarity, unique name, identified), `Base`, `Map`, etc.

## Architecture (Approach A: passive snapshot cache + standalone aggregation window)

**Data flow.** Each frame: if a stash is visible → read current tab (index / name / type).
If the tab just opened or changed (debounced ~300 ms to let it stabilise) **and** prices are
ready → scan `VisibleInventoryItems`, price each item via the bridge, build a `TabSnapshot`,
store it keyed by tab, and write the per-league JSON (debounced write). The aggregation window
runs independently: it reads the store and renders the summary; data collection happens
regardless of whether the window is open.

**Units / conversion.** The bridge returns a raw chaos-equivalent number. Normalize to
exalted by dividing by the raw value of an Exalted Orb base type (`GetBaseItemTypeValue`):
`exalted(item) = raw(item) / raw(ExaltedOrb)`. The divine display ratio is
`divinePerExalted = raw(ExaltedOrb) / raw(DivineOrb)`. The Exalted-Orb probe doubles as the
**prices-ready check**: if `raw(ExaltedOrb)` is 0, price data is not loaded yet → defer
scanning and show "waiting for price data" in the window. Both base-orb raw values are cached
with a short expiry (5 min), matching RitualHelper.

### Data model (persisted JSON, one file per league)

```
StashSnapshot   { League: string, Tabs: List<TabSnapshot> }
TabSnapshot     { Key: int, Name: string, Type: string,
                  LastScannedUtc: DateTime, Items: List<ItemSnapshot> }
ItemSnapshot    { DisplayName: string, GroupKey: string, StackSize: int,
                  UnitValueEx: double, Rarity: string?, Category: string? }
```

- `Key` — **v1: the tab index** (`VisibleStashIndex`), which is always readable and survives
  renames. `Name`/`Type` are stored for display and refreshed on every re-scan. Scope: the
  normal player `StashElement` only (guild stash deferred, to avoid index collisions).
  Limitation: reordering or inserting/removing tabs can shift indices; the next time a tab is
  opened it is re-scanned and overwritten, which self-heals the common cases.
- **Possible later enhancement:** if a stable per-tab id is confirmed readable from
  `ExileCore2.dll` (`StashInventoryId`), switch `Key` to it for reorder-proof identity.
- `UnitValueEx` is the per-one exalted value at scan time; a row's total = `UnitValueEx × StackSize`.
- `GroupKey` is the aggregation key: unique name for uniques, base item name for stackables.
- Snapshots are league-scoped; on league change the plugin loads that league's file.
- **Aggregated row math:** a group's Total = sum of member snapshots' `UnitValueEx × StackSize`;
  the displayed "Unit" = Total / total quantity (an effective/weighted unit, since the same item
  in different tabs may have been scanned at different times/prices). Qty = sum of stack sizes.

### Components / files

| File | Responsibility | ExileCore-dependent |
|---|---|---|
| `StashValueTracker.cs` | Lifecycle (`Initialise/Tick/Render`), orchestration, window toggle | yes |
| `Settings.cs` | `ISettings`: Enable, window hotkey, data path, display options | yes |
| `Pricing/NinjaPricerBridge.cs` | Bridge wrapper: `IsAvailable`, `PriceEntity→ex`, `DivineRate`, `PricesReady` | yes |
| `Scanning/StashScanner.cs` | Detect open stash / current tab, debounce, read items, build `TabSnapshot` | yes |
| `Model/Snapshot.cs` | POCOs: `StashSnapshot/TabSnapshot/ItemSnapshot` | no |
| `Storage/SnapshotStore.cs` | In-memory store keyed by `Key` + JSON load/save (per league), debounced write | no |
| `Aggregation/StashAggregator.cs` | Group by `GroupKey`, sums, grand total, divine conversion, filter unpriced | no |
| `Formatting/CurrencyFormat.cs` | `"X ex (~Y div)"`, number formatting | no |
| `UI/ValueWindow.cs` | ImGui window: tab filter panel + summary table + total | yes |
| `tests/…` | xUnit (`net8.0`) for Aggregator / Store / Format | no |

ExileCore-free files (Model / Storage / Aggregation / Formatting) are isolated so a plain
`net8.0` xUnit project can compile and run real tests on Linux. The plugin assembly targets
`net8.0-windows` and builds on Linux for compile-verification (`EnableWindowsTargeting`,
reference DLLs in a gitignored `lib/`), but only runs in-game on Windows.

### UI (ImGui window, opened by a "Show window" toggle; data collection is independent of the window)

```
┌─ Stash Value Tracker ─────────────────────────────────────────────┐
│ Total (selected): 1 240 ex (~8.7 div)        • 14 items unpriced   │
├──────────────┬────────────────────────────────────────────────────┤
│ Tabs         │ Item          Tab         Qty    Unit        Total   │
│ [x] Currency │ Divine Orb    Currency     42  1 ex(~1d)  42 ex(~0.3)│
│   312ex 2m   │ Exalted Orb   Currency+2 1240  1 ex(~0d) 1240 ex(8.7)│
│ [x] Frags    │ Headhunter    Uniques       1  900 ex     900 ex(6.3)│
│   88ex 5m    │ …                                                    │
│ [ ] Maps     │ (click any column header to sort)                    │
│   — not scan │                                                      │
│ [x] Uniques  │                                                      │
│   840ex 1m   │                                                      │
└──────────────┴────────────────────────────────────────────────────┘
```

- **Left panel = tab list + filter:** per tab a checkbox, name, its summed value, and
  "scanned N ago" (or "not scanned"). This satisfies both "remember tabs / names / scan time"
  and the "which tabs to include" filter in one place.
- **Right table = the summary:** one row per item; columns Item / Tab / Qty / Unit / Total.
  The **Tab** column shows the single source tab, or `Currency +2` with a hover tooltip
  listing all tabs when the item spans several. Sortable columns; default sort by Total desc.
  Every value is formatted `X ex (~Y div)`.
- **Header:** grand total over selected tabs + unpriced-item count.
- **Controls:** "Forget tab" (drop a stale tab from the store). No "Refresh" button — re-scan
  only happens by opening the tab in-game; a hint conveys this.

### Settings

- `Enable` (toggle)
- `Show window` (toggle — opens/closes the aggregation window). A WinForms `HotkeyNode` is
  intentionally avoided: it pulls in `System.Windows.Forms`, which breaks the plugin
  assembly's Linux compile-verification (the build gate our process relies on). A hotkey is a
  possible later enhancement, added/tested on Windows.
- `Scan debounce (ms)` (default 300)
- Data storage path (default: plugin folder)

Unpriced items (value 0) are always hidden and only counted in the footer — per the chosen
"hide" behaviour — so there is no per-item visibility toggle.

## Error handling

- **NinjaPricer not loaded:** window shows a banner ("NinjaPricer not loaded — valuation
  unavailable"); scanning is skipped (no values to record).
- **Prices not ready:** Exalted-Orb probe returns 0 → defer scanning, show "waiting for price
  data". Prevents recording all-zero snapshots.
- **Tab read exception:** wrapped in try/catch; skip that scan, keep prior snapshot.
- **Corrupt JSON on load:** ignore the file, start fresh, log via `LogError`.
- **Stale tabs (deleted in game):** remain in the store; "Forget tab" removes them; names are
  reconciled on re-scan.
- **League change:** load the snapshot file for the new league.

## Testing

- **Unit (net8.0, runs on Linux):** `StashAggregator` (grouping, sums, grand total, threshold,
  divine conversion, multi-tab tab-column logic), `SnapshotStore` (JSON round-trip, per-league
  files, corrupt-file handling), `CurrencyFormat` (`X ex (~Y div)` formatting / rounding).
- **Manual in-game (Windows):** scanning on tab open/change, debounce, prices-ready gating,
  persistence across restart, NinjaPricer-absent banner, window filter/sort behaviour.

## Out of scope (v1, YAGNI)

- Own price source / fallback when NinjaPricer is absent.
- Live re-pricing of stackables (Approach C) — snapshot-at-scan only.
- Reading closed tabs' contents (not possible via the API).
- Pricing rares/uniques the bridge cannot price.
- Guild stash support (normal player stash only in v1).
- A WinForms toggle hotkey (would break Linux compile-verification; deferred to a
  Windows-only follow-up).
- Stable per-tab id for `Key` (uses tab index in v1).
