# Stash Value Tracker Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build an ExileCore2 (PoE2) plugin that passively remembers each opened stash tab's contents and value (priced via NinjaPricer's PluginBridge) and shows the combined worth of all tabs as a filterable, per-item summary window.

**Architecture:** Pure-logic core (model, currency formatting, aggregation, JSON persistence) lives in ExileCore-free files so it is unit-tested on Linux with xUnit. ExileCore-dependent pieces (NinjaPricer bridge wrapper, stash scanner, ImGui window, plugin orchestration) are compile-verified on Linux (`EnableWindowsTargeting` + reference DLLs) and manually verified in-game on Windows. The plugin scans only the currently visible tab, keyed by tab index, snapshotting exalted values at scan time; re-opening a tab re-scans it.

**Tech Stack:** C# / .NET 8 (`net8.0-windows` plugin, `net8.0` tests), ExileCore2 API, ImGui.NET, System.Text.Json, xUnit.

**Reference (read once before starting):**
- Spec: `docs/superpowers/specs/2026-06-14-stash-value-tracker-design.md`
- NinjaPricer bridge registration: `~/Work/My/ExileCore2_05/Plugins/Source/NinjaPricer/NinjaPricer.cs:59-70`
- Bridge consumption + chaos→exalted normalization: `~/Work/My/ExileCore2_05/Plugins/Source/RitualHelper/NinjaPricerBridgeService.cs:116-175` and `RitualHelper.cs:224-243, 851-862`
- Stash reading: `~/Work/My/ExileCore2_05/Plugins/Source/NinjaPricer/Render.cs:119-153`; `~/Work/My/ExileCore2_05/Plugins/Source/HighlightedItems/HighlightedItems.cs:205-247`
- Item components: `~/Work/My/ExileCore2_05/Plugins/Source/NinjaPricer/CustomItem.cs:1-9, 109-143`
- ImGui table: `~/Work/My/ExileCore2_05/Plugins/Source/ExileMaps/ExileMaps.Waypoints.cs:227-250`
- Plugin csproj baseline: `~/Work/My/EssenceHelper/EssenceHelper.csproj`
- Cross-build notes: memory `exilecore2-plugin-cross-build`, `dotnet-sdk-install`.

**Conventions:**
- Repo `~/Work/My/StashValueTracker` (default branch `master`). **Create a feature branch before any code** (Task 1).
- `lib/` (reference DLLs) is gitignored — never commit it.
- Commit messages end with: `Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>`
- All namespaces rooted at `StashValueTracker`.

---

## File structure

| File | Responsibility | ExileCore-dependent |
|---|---|---|
| `StashValueTracker.csproj` | Plugin assembly (`net8.0-windows`) | — |
| `tests/StashValueTracker.Tests/StashValueTracker.Tests.csproj` | xUnit project (`net8.0`), links pure files | — |
| `Model/Snapshot.cs` | POCOs `ItemSnapshot/TabSnapshot/StashSnapshot` | no |
| `Formatting/CurrencyFormat.cs` | `"X ex (~Y div)"` formatting | no |
| `Aggregation/StashAggregator.cs` | Group/sum across tabs, grand total, unpriced count | no |
| `Storage/SnapshotStore.cs` | In-memory store + per-league JSON load/save | no |
| `Pricing/NinjaPricerBridge.cs` | Bridge wrapper: availability, exalted value, divine ratio, readiness | yes |
| `Scanning/StashScanner.cs` | Read visible tab → `TabSnapshot` | yes |
| `UI/ValueWindow.cs` | ImGui window: tab filter + summary table | yes |
| `Settings.cs` | `ISettings` nodes | yes |
| `StashValueTracker.cs` | Lifecycle + orchestration | yes |

---

### Task 1: Project scaffold + build harness

**Files:**
- Create: `StashValueTracker.csproj`
- Create: `tests/StashValueTracker.Tests/StashValueTracker.Tests.csproj`
- Create: `tests/StashValueTracker.Tests/SmokeTest.cs`
- Create: `lib/` (populated locally, gitignored)

- [ ] **Step 1: Create the feature branch**

```bash
cd ~/Work/My/StashValueTracker
git checkout -b feature/v1-implementation
```

- [ ] **Step 2: Populate `lib/` with reference DLLs (local only, not committed)**

```bash
cd ~/Work/My/StashValueTracker
mkdir -p lib
cp ~/Work/My/ExileCore2_05/ExileCore2.dll lib/ 2>/dev/null || true
for d in GameOffsets2.dll ItemFilterLibrary.dll; do
  f=$(find ~/Work/My/ExileCore2_05 -name "$d" 2>/dev/null | head -1)
  [ -n "$f" ] && cp "$f" lib/
done
ls lib
```
Expected: `lib` contains at least `ExileCore2.dll`, `GameOffsets2.dll`, `ItemFilterLibrary.dll`. If any is missing, locate it under `~/Work/My/ExileCore2_05` (e.g. `Plugins/Temp/<Plugin>/`) and copy it in.

- [ ] **Step 3: Write the plugin csproj**

`StashValueTracker.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <OutputType>Library</OutputType>
    <EnableWindowsTargeting>true</EnableWindowsTargeting>
    <PlatformTarget>x64</PlatformTarget>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <DebugType>embedded</DebugType>
    <AssemblyName>StashValueTracker</AssemblyName>
    <RootNamespace>StashValueTracker</RootNamespace>
    <OutputPath Condition="'$(ExApiPluginOutputPath)' != ''">$(ExApiPluginOutputPath)$(MSBuildProjectName)</OutputPath>
  </PropertyGroup>

  <ItemGroup>
    <Compile Remove="tests\**\*.cs" />
    <Content Remove="tests\**\*" />
    <None Remove="tests\**\*" />
  </ItemGroup>

  <ItemGroup>
    <Reference Include="ExileCore2">
      <HintPath>$(exileCore2Package)\ExileCore2.dll</HintPath>
      <Private>False</Private>
    </Reference>
    <Reference Include="GameOffsets2">
      <HintPath>$(exileCore2Package)\GameOffsets2.dll</HintPath>
      <Private>False</Private>
    </Reference>
    <Reference Include="ItemFilterLibrary">
      <HintPath>$(exileCore2Package)\ItemFilterLibrary.dll</HintPath>
      <Private>False</Private>
    </Reference>
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="ImGui.NET" Version="1.90.0.1" />
  </ItemGroup>
</Project>
```

- [ ] **Step 4: Write the test csproj**

`tests/StashValueTracker.Tests/StashValueTracker.Tests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
  </PropertyGroup>

  <ItemGroup>
    <!-- Test sources. ExileCore-free production files are linked here incrementally, one per
         task (Tasks 2-5), so the project always compiles only files that already exist.
         No project reference — that would pull in the net8.0-windows plugin assembly. -->
    <Compile Include="*.cs" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
  </ItemGroup>
</Project>
```

- [ ] **Step 5: Write a smoke test**

`tests/StashValueTracker.Tests/SmokeTest.cs`:
```csharp
using Xunit;

namespace StashValueTracker.Tests;

public class SmokeTest
{
    [Fact]
    public void Sanity()
    {
        Assert.Equal(4, 2 + 2);
    }
}
```

- [ ] **Step 6: Verify both projects build/test on Linux**

Run:
```bash
cd ~/Work/My/StashValueTracker
dotnet test tests/StashValueTracker.Tests/StashValueTracker.Tests.csproj 2>&1 | tail -6
```
Expected: `SmokeTest.Sanity` passes (no production files linked yet).

- [ ] **Step 7: Verify the plugin assembly restores/builds on Linux**

Run:
```bash
cd ~/Work/My/StashValueTracker
exileCore2Package="$HOME/Work/My/StashValueTracker/lib" dotnet build StashValueTracker.csproj -c Debug 2>&1 | tail -5
```
Expected: `Build succeeded.` (0 source files compiled yet is fine — it proves references/restore work). If restore fails on `System.Windows.Forms`, confirm no WinForms usage and that `UseWindowsForms` is absent.

- [ ] **Step 8: Commit**

```bash
cd ~/Work/My/StashValueTracker
git add StashValueTracker.csproj tests/
git commit -m "$(printf 'chore: scaffold plugin and test projects\n\nCo-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>')"
```

---

### Task 2: Data model POCOs

**Files:**
- Create: `Model/Snapshot.cs`

- [ ] **Step 1: Write the model**

`Model/Snapshot.cs`:
```csharp
using System;
using System.Collections.Generic;

namespace StashValueTracker.Model;

public sealed class ItemSnapshot
{
    public string DisplayName { get; set; } = "";
    public string GroupKey { get; set; } = "";
    public int StackSize { get; set; }
    public double UnitValueEx { get; set; }   // exalted per single item, captured at scan time
    public string? Rarity { get; set; }
    public string? Category { get; set; }
}

public sealed class TabSnapshot
{
    public int Key { get; set; }              // tab index (v1 identity)
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public DateTime LastScannedUtc { get; set; }
    public List<ItemSnapshot> Items { get; set; } = new();
}

public sealed class StashSnapshot
{
    public string League { get; set; } = "";
    public List<TabSnapshot> Tabs { get; set; } = new();
}
```

- [ ] **Step 2: Link the model into the test project**

In `tests/StashValueTracker.Tests/StashValueTracker.Tests.csproj`, add this line inside the
test-sources `<ItemGroup>` (above `<Compile Include="*.cs" />`):
```xml
    <Compile Include="..\..\Model\Snapshot.cs" Link="Model\Snapshot.cs" />
```

- [ ] **Step 3: Verify both projects build**

Run:
```bash
cd ~/Work/My/StashValueTracker
dotnet test tests/StashValueTracker.Tests/StashValueTracker.Tests.csproj 2>&1 | tail -6
exileCore2Package="$HOME/Work/My/StashValueTracker/lib" dotnet build StashValueTracker.csproj -c Debug 2>&1 | tail -5
```
Expected: `SmokeTest.Sanity` passes; `Build succeeded.`

- [ ] **Step 4: Commit**

```bash
git add Model/Snapshot.cs tests/StashValueTracker.Tests/StashValueTracker.Tests.csproj
git commit -m "$(printf 'feat: add snapshot data model\n\nCo-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>')"
```

---

### Task 3: Currency formatting (TDD)

**Files:**
- Create: `Formatting/CurrencyFormat.cs`
- Test: `tests/StashValueTracker.Tests/CurrencyFormatTests.cs`

- [ ] **Step 1: Write the failing tests**

`tests/StashValueTracker.Tests/CurrencyFormatTests.cs`:
```csharp
using StashValueTracker.Formatting;
using Xunit;

namespace StashValueTracker.Tests;

public class CurrencyFormatTests
{
    [Theory]
    [InlineData(1240, "1240")]
    [InlineData(42, "42")]
    [InlineData(8.68, "8.7")]
    [InlineData(0.3, "0.3")]
    [InlineData(0.04, "0.04")]
    public void FormatNumber_uses_scaled_precision(double value, string expected)
    {
        Assert.Equal(expected, CurrencyFormat.FormatNumber(value));
    }

    [Fact]
    public void ExWithDiv_appends_divine_suffix()
    {
        Assert.Equal("1240 ex (~8.7 div)", CurrencyFormat.ExWithDiv(1240, 0.007));
        Assert.Equal("900 ex (~6.3 div)", CurrencyFormat.ExWithDiv(900, 0.007));
    }

    [Fact]
    public void ExWithDiv_omits_divine_when_ratio_unknown()
    {
        Assert.Equal("42 ex", CurrencyFormat.ExWithDiv(42, 0));
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd ~/Work/My/StashValueTracker && dotnet test tests/StashValueTracker.Tests/StashValueTracker.Tests.csproj 2>&1 | tail -8`
Expected: FAIL — `CurrencyFormat` does not exist (compile error).

- [ ] **Step 3: Implement (and link the file into the test project)**

First add this line to the test-sources `<ItemGroup>` in
`tests/StashValueTracker.Tests/StashValueTracker.Tests.csproj`:
```xml
    <Compile Include="..\..\Formatting\CurrencyFormat.cs" Link="Formatting\CurrencyFormat.cs" />
```
Then create `Formatting/CurrencyFormat.cs`:
```csharp
using System;
using System.Globalization;

namespace StashValueTracker.Formatting;

public static class CurrencyFormat
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static string FormatNumber(double value)
    {
        var a = Math.Abs(value);
        if (a >= 100) return value.ToString("0", Inv);
        if (a >= 1) return value.ToString("0.#", Inv);
        return value.ToString("0.##", Inv);
    }

    public static string ExWithDiv(double exalted, double divinePerExalted)
    {
        var ex = FormatNumber(exalted);
        if (divinePerExalted <= 0) return ex + " ex";
        var div = exalted * divinePerExalted;
        return $"{ex} ex (~{FormatNumber(div)} div)";
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd ~/Work/My/StashValueTracker && dotnet test tests/StashValueTracker.Tests/StashValueTracker.Tests.csproj 2>&1 | tail -8`
Expected: PASS (all CurrencyFormat tests + SmokeTest).

- [ ] **Step 5: Commit**

```bash
git add Formatting/CurrencyFormat.cs tests/StashValueTracker.Tests/CurrencyFormatTests.cs tests/StashValueTracker.Tests/StashValueTracker.Tests.csproj
git commit -m "$(printf 'feat: add exalted/divine currency formatting\n\nCo-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>')"
```

---

### Task 4: Stash aggregator (TDD)

**Files:**
- Create: `Aggregation/StashAggregator.cs`
- Test: `tests/StashValueTracker.Tests/StashAggregatorTests.cs`

- [ ] **Step 1: Write the failing tests**

`tests/StashValueTracker.Tests/StashAggregatorTests.cs`:
```csharp
using System.Collections.Generic;
using StashValueTracker.Aggregation;
using StashValueTracker.Model;
using Xunit;

namespace StashValueTracker.Tests;

public class StashAggregatorTests
{
    private static TabSnapshot Tab(int key, string name, params ItemSnapshot[] items) =>
        new() { Key = key, Name = name, Items = new List<ItemSnapshot>(items) };

    private static ItemSnapshot Item(string name, int stack, double unitEx) =>
        new() { DisplayName = name, GroupKey = name, StackSize = stack, UnitValueEx = unitEx };

    [Fact]
    public void Merges_same_item_across_tabs_and_sums_qty_and_total()
    {
        var tabs = new[]
        {
            Tab(0, "Currency", Item("Exalted Orb", 100, 1)),
            Tab(1, "Frags", Item("Exalted Orb", 40, 1)),
        };

        var result = StashAggregator.Aggregate(tabs, new HashSet<int> { 0, 1 });

        var row = Assert.Single(result.Rows);
        Assert.Equal("Exalted Orb", row.DisplayName);
        Assert.Equal(140, row.Quantity);
        Assert.Equal(140, row.TotalEx);
        Assert.Equal(new[] { "Currency", "Frags" }, row.TabNames);
        Assert.Equal("Currency +1", row.TabLabel);
    }

    [Fact]
    public void Single_tab_item_shows_plain_tab_label()
    {
        var tabs = new[] { Tab(2, "Uniques", Item("Headhunter", 1, 900)) };
        var result = StashAggregator.Aggregate(tabs, new HashSet<int> { 2 });
        var row = Assert.Single(result.Rows);
        Assert.Equal("Uniques", row.TabLabel);
        Assert.Equal(900, row.UnitEx);
    }

    [Fact]
    public void Excludes_and_counts_unpriced_items()
    {
        var tabs = new[]
        {
            Tab(0, "Currency", Item("Exalted Orb", 10, 1), Item("Rare Ring", 1, 0)),
        };
        var result = StashAggregator.Aggregate(tabs, new HashSet<int> { 0 });
        Assert.Single(result.Rows);
        Assert.Equal(1, result.UnpricedCount);
        Assert.Equal(10, result.GrandTotalEx);
    }

    [Fact]
    public void Respects_include_filter()
    {
        var tabs = new[]
        {
            Tab(0, "Currency", Item("Exalted Orb", 10, 1)),
            Tab(1, "Maps", Item("Divine Orb", 5, 200)),
        };
        var result = StashAggregator.Aggregate(tabs, new HashSet<int> { 0 });
        var row = Assert.Single(result.Rows);
        Assert.Equal("Exalted Orb", row.DisplayName);
        Assert.Equal(10, result.GrandTotalEx);
    }

    [Fact]
    public void Empty_filter_yields_no_rows()
    {
        var tabs = new[] { Tab(0, "Currency", Item("Exalted Orb", 10, 1)) };
        var result = StashAggregator.Aggregate(tabs, new HashSet<int>());
        Assert.Empty(result.Rows);
        Assert.Equal(0, result.GrandTotalEx);
    }

    [Fact]
    public void Rows_sorted_by_total_descending()
    {
        var tabs = new[]
        {
            Tab(0, "A", Item("Cheap", 1, 5), Item("Pricey", 1, 500)),
        };
        var result = StashAggregator.Aggregate(tabs, new HashSet<int> { 0 });
        Assert.Equal("Pricey", result.Rows[0].DisplayName);
        Assert.Equal("Cheap", result.Rows[1].DisplayName);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd ~/Work/My/StashValueTracker && dotnet test tests/StashValueTracker.Tests/StashValueTracker.Tests.csproj 2>&1 | tail -8`
Expected: FAIL — `StashAggregator`/`AggregatedRow`/`AggregationResult` do not exist.

- [ ] **Step 3: Implement (and link the file into the test project)**

First add this line to the test-sources `<ItemGroup>` in
`tests/StashValueTracker.Tests/StashValueTracker.Tests.csproj`:
```xml
    <Compile Include="..\..\Aggregation\StashAggregator.cs" Link="Aggregation\StashAggregator.cs" />
```
Then create `Aggregation/StashAggregator.cs`:
```csharp
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
}

public static class StashAggregator
{
    public static AggregationResult Aggregate(IEnumerable<TabSnapshot> tabs, ISet<int> includeKeys)
    {
        var included = (tabs ?? Enumerable.Empty<TabSnapshot>())
            .Where(t => includeKeys != null && includeKeys.Contains(t.Key))
            .OrderBy(t => t.Key)
            .ToList();

        var unpriced = 0;
        var groups = new Dictionary<string, GroupAccum>();

        foreach (var tab in included)
        {
            foreach (var item in tab.Items ?? new List<ItemSnapshot>())
            {
                if (item.UnitValueEx <= 0) { unpriced++; continue; }

                if (!groups.TryGetValue(item.GroupKey, out var acc))
                {
                    acc = new GroupAccum { DisplayName = item.DisplayName, GroupKey = item.GroupKey };
                    groups[item.GroupKey] = acc;
                }

                acc.Quantity += item.StackSize;
                acc.TotalEx += item.UnitValueEx * item.StackSize;
                if (!acc.TabNames.Contains(tab.Name)) acc.TabNames.Add(tab.Name);
            }
        }

        var rows = groups.Values
            .Select(g => new AggregatedRow
            {
                DisplayName = g.DisplayName,
                GroupKey = g.GroupKey,
                Quantity = g.Quantity,
                TotalEx = g.TotalEx,
                TabNames = g.TabNames,
            })
            .OrderByDescending(r => r.TotalEx)
            .ToList();

        return new AggregationResult
        {
            Rows = rows,
            GrandTotalEx = rows.Sum(r => r.TotalEx),
            UnpricedCount = unpriced,
        };
    }

    private sealed class GroupAccum
    {
        public string DisplayName = "";
        public string GroupKey = "";
        public int Quantity;
        public double TotalEx;
        public readonly List<string> TabNames = new();
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd ~/Work/My/StashValueTracker && dotnet test tests/StashValueTracker.Tests/StashValueTracker.Tests.csproj 2>&1 | tail -8`
Expected: PASS (all StashAggregator tests).

- [ ] **Step 5: Commit**

```bash
git add Aggregation/StashAggregator.cs tests/StashValueTracker.Tests/StashAggregatorTests.cs tests/StashValueTracker.Tests/StashValueTracker.Tests.csproj
git commit -m "$(printf 'feat: add cross-tab item aggregator\n\nCo-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>')"
```

---

### Task 5: Snapshot store with JSON persistence (TDD)

**Files:**
- Create: `Storage/SnapshotStore.cs`
- Test: `tests/StashValueTracker.Tests/SnapshotStoreTests.cs`

- [ ] **Step 1: Write the failing tests**

`tests/StashValueTracker.Tests/SnapshotStoreTests.cs`:
```csharp
using System.Collections.Generic;
using System.IO;
using StashValueTracker.Model;
using StashValueTracker.Storage;
using Xunit;

namespace StashValueTracker.Tests;

public class SnapshotStoreTests
{
    private static string TempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "svt_test_" + System.Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static TabSnapshot SampleTab(int key) => new()
    {
        Key = key, Name = "Currency", Type = "CurrencyStash",
        Items = new List<ItemSnapshot> { new() { DisplayName = "Exalted Orb", GroupKey = "Exalted Orb", StackSize = 5, UnitValueEx = 1 } },
    };

    [Fact]
    public void Save_then_reload_round_trips_tabs()
    {
        var dir = TempDir();
        var store = new SnapshotStore(dir);
        store.LoadLeague("Standard");
        store.UpsertTab(SampleTab(0));
        store.Save();

        var reloaded = new SnapshotStore(dir);
        reloaded.LoadLeague("Standard");

        var tab = Assert.Single(reloaded.Tabs);
        Assert.Equal("Currency", tab.Name);
        Assert.Equal("Exalted Orb", Assert.Single(tab.Items).DisplayName);
    }

    [Fact]
    public void Upsert_replaces_tab_with_same_key()
    {
        var store = new SnapshotStore(TempDir());
        store.LoadLeague("Standard");
        store.UpsertTab(SampleTab(0));
        store.UpsertTab(new TabSnapshot { Key = 0, Name = "Renamed" });
        var tab = Assert.Single(store.Tabs);
        Assert.Equal("Renamed", tab.Name);
    }

    [Fact]
    public void ForgetTab_removes_by_key()
    {
        var store = new SnapshotStore(TempDir());
        store.LoadLeague("Standard");
        store.UpsertTab(SampleTab(0));
        store.UpsertTab(SampleTab(1));
        store.ForgetTab(0);
        var tab = Assert.Single(store.Tabs);
        Assert.Equal(1, tab.Key);
    }

    [Fact]
    public void Leagues_are_stored_in_separate_files()
    {
        var dir = TempDir();
        var store = new SnapshotStore(dir);
        store.LoadLeague("Standard");
        store.UpsertTab(SampleTab(0));
        store.Save();
        store.LoadLeague("Hardcore");
        Assert.Empty(store.Tabs);
    }

    [Fact]
    public void Corrupt_file_loads_as_empty_without_throwing()
    {
        var dir = TempDir();
        File.WriteAllText(Path.Combine(dir, "Standard.json"), "{ this is not valid json");
        var store = new SnapshotStore(dir);
        store.LoadLeague("Standard");
        Assert.Empty(store.Tabs);
        Assert.Equal("Standard", store.League);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd ~/Work/My/StashValueTracker && dotnet test tests/StashValueTracker.Tests/StashValueTracker.Tests.csproj 2>&1 | tail -8`
Expected: FAIL — `SnapshotStore` does not exist.

- [ ] **Step 3: Implement (and link the file into the test project)**

First add this line to the test-sources `<ItemGroup>` in
`tests/StashValueTracker.Tests/StashValueTracker.Tests.csproj`:
```xml
    <Compile Include="..\..\Storage\SnapshotStore.cs" Link="Storage\SnapshotStore.cs" />
```
Then create `Storage/SnapshotStore.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using StashValueTracker.Model;

namespace StashValueTracker.Storage;

public sealed class SnapshotStore
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private readonly string _dataDir;
    private readonly Action<string>? _logError;
    private StashSnapshot _current = new();

    public SnapshotStore(string dataDir, Action<string>? logError = null)
    {
        _dataDir = dataDir;
        _logError = logError;
    }

    public string League => _current.League;
    public IReadOnlyList<TabSnapshot> Tabs => _current.Tabs;

    public void LoadLeague(string league)
    {
        league ??= "";
        var path = PathFor(league);
        if (!File.Exists(path))
        {
            _current = new StashSnapshot { League = league };
            return;
        }

        try
        {
            var json = File.ReadAllText(path);
            var loaded = JsonSerializer.Deserialize<StashSnapshot>(json);
            _current = loaded ?? new StashSnapshot();
            _current.League = league;
            _current.Tabs ??= new List<TabSnapshot>();
        }
        catch (Exception ex)
        {
            _logError?.Invoke($"failed to load snapshot for '{league}': {ex.Message}");
            _current = new StashSnapshot { League = league };
        }
    }

    public void UpsertTab(TabSnapshot tab)
    {
        if (tab == null) return;
        _current.Tabs.RemoveAll(t => t.Key == tab.Key);
        _current.Tabs.Add(tab);
    }

    public void ForgetTab(int key) => _current.Tabs.RemoveAll(t => t.Key == key);

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(_dataDir);
            var json = JsonSerializer.Serialize(_current, JsonOpts);
            File.WriteAllText(PathFor(_current.League), json);
        }
        catch (Exception ex)
        {
            _logError?.Invoke($"failed to save snapshot: {ex.Message}");
        }
    }

    private string PathFor(string league) => Path.Combine(_dataDir, Sanitize(league) + ".json");

    private static string Sanitize(string s)
    {
        if (string.IsNullOrEmpty(s)) return "Standard";
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new StringBuilder(s.Length);
        foreach (var c in s) sb.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
        return sb.ToString();
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd ~/Work/My/StashValueTracker && dotnet test tests/StashValueTracker.Tests/StashValueTracker.Tests.csproj 2>&1 | tail -8`
Expected: PASS (all tests across all suites).

- [ ] **Step 5: Commit**

```bash
git add Storage/SnapshotStore.cs tests/StashValueTracker.Tests/SnapshotStoreTests.cs tests/StashValueTracker.Tests/StashValueTracker.Tests.csproj
git commit -m "$(printf 'feat: add per-league snapshot store with JSON persistence\n\nCo-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>')"
```

---

### Task 6: Settings

**Files:**
- Create: `Settings.cs`

> ExileCore-dependent — no unit test; the gate is a clean plugin build. Do NOT add a
> `HotkeyNode` (pulls in `System.Windows.Forms` and breaks the Linux build).

- [ ] **Step 1: Implement**

`Settings.cs`:
```csharp
using ExileCore2.Shared.Attributes;
using ExileCore2.Shared.Interfaces;
using ExileCore2.Shared.Nodes;

namespace StashValueTracker;

public class Settings : ISettings
{
    public ToggleNode Enable { get; set; } = new(true);

    [Menu("Show value window", "Open/close the aggregated stash value window.")]
    public ToggleNode ShowWindow { get; set; } = new(false);

    [Menu("Scan debounce (ms)", "How long a tab must stay open before it is scanned.")]
    public RangeNode<int> ScanDebounceMs { get; set; } = new(300, 0, 2000);
}
```

- [ ] **Step 2: Verify the plugin builds**

Run:
```bash
cd ~/Work/My/StashValueTracker
exileCore2Package="$HOME/Work/My/StashValueTracker/lib" dotnet build StashValueTracker.csproj -c Debug 2>&1 | tail -5
```
Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git add Settings.cs
git commit -m "$(printf 'feat: add plugin settings\n\nCo-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>')"
```

---

### Task 7: NinjaPricer bridge wrapper

**Files:**
- Create: `Pricing/NinjaPricerBridge.cs`

> ExileCore-dependent — gate is a clean build. Mirrors RitualHelper's normalization
> (`NinjaPricerBridgeService.cs:116-175`, `RitualHelper.cs:236-237, 851-862`).

- [ ] **Step 1: Implement**

`Pricing/NinjaPricerBridge.cs`:
```csharp
using System;
using System.Linq;
using ExileCore2;
using ExileCore2.PoEMemory.MemoryObjects;
using ExileCore2.PoEMemory.Models;

namespace StashValueTracker.Pricing;

/// <summary>Wraps NinjaPricer's PluginBridge methods and normalizes raw values to exalted.</summary>
public sealed class NinjaPricerBridge
{
    private readonly GameController _gc;
    private readonly Action<string> _logError;

    private Func<Entity, double>? _getEntityValue;
    private Func<BaseItemType, double>? _getBaseValue;
    private DateTime _nextRetry = DateTime.MinValue;

    private readonly TimeSpan _ratesExpiry = TimeSpan.FromMinutes(5);
    private DateTime _ratesRefreshedAt = DateTime.MinValue;
    private double _rawPerExalted;
    private double _rawPerDivine;

    public NinjaPricerBridge(GameController gc, Action<string> logError)
    {
        _gc = gc;
        _logError = logError;
    }

    public bool IsAvailable => EnsureMethods();

    public bool PricesReady
    {
        get { RefreshRates(); return _rawPerExalted > 0; }
    }

    public double DivinePerExalted
    {
        get { RefreshRates(); return _rawPerDivine > 0 ? _rawPerExalted / _rawPerDivine : 0; }
    }

    /// <summary>Exalted value of a live item entity (already accounts for stack size). 0 if unavailable/unpriced.</summary>
    public double ExaltedValueOfStack(Entity item)
    {
        if (item == null || !EnsureMethods()) return 0;
        RefreshRates();
        if (_rawPerExalted <= 0) return 0;
        try
        {
            var raw = _getEntityValue!(item);
            return raw > 0 ? raw / _rawPerExalted : 0;
        }
        catch (Exception ex)
        {
            _logError($"error pricing item: {ex.Message}");
            return 0;
        }
    }

    private bool EnsureMethods()
    {
        if (_getEntityValue != null && _getBaseValue != null) return true;
        if (DateTime.Now < _nextRetry) return false;

        _getEntityValue ??= _gc.PluginBridge.GetMethod<Func<Entity, double>>("NinjaPrice.GetValue");
        _getBaseValue ??= _gc.PluginBridge.GetMethod<Func<BaseItemType, double>>("NinjaPrice.GetBaseItemTypeValue");

        if (_getEntityValue == null || _getBaseValue == null)
        {
            _nextRetry = DateTime.Now.AddSeconds(2);
            return false;
        }
        return true;
    }

    private void RefreshRates()
    {
        if (!EnsureMethods()) return;
        if (DateTime.Now - _ratesRefreshedAt < _ratesExpiry && _rawPerExalted > 0) return;

        try
        {
            var exalted = FindBase("Exalted Orb");
            var divine = FindBase("Divine Orb");
            if (exalted != null)
            {
                var v = _getBaseValue!(exalted);
                if (v > 0) _rawPerExalted = v;
            }
            if (divine != null)
            {
                var v = _getBaseValue!(divine);
                if (v > 0) _rawPerDivine = v;
            }
            if (_rawPerExalted > 0) _ratesRefreshedAt = DateTime.Now;
        }
        catch (Exception ex)
        {
            _logError($"error refreshing orb rates: {ex.Message}");
        }
    }

    private BaseItemType? FindBase(string baseName) =>
        _gc.Files.BaseItemTypes.Contents.Values
            .FirstOrDefault(b => b != null && string.Equals(b.BaseName, baseName, StringComparison.OrdinalIgnoreCase));
}
```

- [ ] **Step 2: Verify the plugin builds**

Run:
```bash
cd ~/Work/My/StashValueTracker
exileCore2Package="$HOME/Work/My/StashValueTracker/lib" dotnet build StashValueTracker.csproj -c Debug 2>&1 | tail -8
```
Expected: `Build succeeded.` If `Entity` or `BaseItemType` do not resolve, fix the `using` (candidates seen in the codebase: `ExileCore2.PoEMemory`, `ExileCore2.PoEMemory.MemoryObjects`, `ExileCore2.PoEMemory.Models`) and rebuild.

- [ ] **Step 3: Commit**

```bash
git add Pricing/NinjaPricerBridge.cs
git commit -m "$(printf 'feat: add NinjaPricer bridge wrapper with exalted normalization\n\nCo-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>')"
```

---

### Task 8: Stash scanner

**Files:**
- Create: `Scanning/StashScanner.cs`

> ExileCore-dependent — gate is a clean build + in-game verification later.
> `StashElement.VisibleStashIndex` and `.TabName` are observed in `ExileCore2.dll`
> (`get_VisibleStashIndex`, `get_TabName`). If a member name differs at compile time,
> adjust to the actual member and keep the same behavior.

- [ ] **Step 1: Implement**

`Scanning/StashScanner.cs`:
```csharp
using System;
using System.Collections.Generic;
using ExileCore2;
using ExileCore2.PoEMemory.Components;
using ExileCore2.PoEMemory.Elements.InventoryElements;
using ExileCore2.PoEMemory.MemoryObjects;
using StashValueTracker.Model;
using StashValueTracker.Pricing;

namespace StashValueTracker.Scanning;

public sealed class StashScanner
{
    private readonly GameController _gc;
    private readonly NinjaPricerBridge _bridge;
    private readonly Action<string> _logError;

    public StashScanner(GameController gc, NinjaPricerBridge bridge, Action<string> logError)
    {
        _gc = gc;
        _bridge = bridge;
        _logError = logError;
    }

    /// <summary>The visible normal player stash, or null when no stash tab is open.</summary>
    public StashElement? GetVisibleStash()
    {
        var stash = _gc.IngameState.IngameUi.StashElement;
        return stash is { IsVisible: true } ? stash : null;
    }

    /// <summary>Index of the currently visible tab, or null.</summary>
    public int? CurrentTabIndex() => GetVisibleStash()?.VisibleStashIndex;

    /// <summary>Snapshot the currently visible tab. Returns null if nothing is scannable.</summary>
    public TabSnapshot? ScanCurrentTab(DateTime nowUtc)
    {
        var stash = GetVisibleStash();
        var inventory = stash?.VisibleStash;
        var items = inventory?.VisibleInventoryItems;
        if (stash == null || inventory == null || items == null) return null;

        var index = stash.VisibleStashIndex;
        var snapshot = new TabSnapshot
        {
            Key = index,
            Name = string.IsNullOrWhiteSpace(stash.TabName) ? $"Tab {index}" : stash.TabName,
            Type = inventory.InvType.ToString(),
            LastScannedUtc = nowUtc,
            Items = new List<ItemSnapshot>(),
        };

        foreach (var slot in items)
        {
            try
            {
                var entity = slot?.Item;
                if (entity == null || !entity.IsValid) continue;

                var stackValueEx = _bridge.ExaltedValueOfStack(entity);
                var stackSize = entity.TryGetComponent<Stack>(out var stack) ? Math.Max(1, stack.Size) : 1;
                var baseType = _gc.Files.BaseItemTypes.Translate(entity.Path);
                var baseName = baseType?.BaseName ?? entity.Path;

                string display = baseName;
                string groupKey = baseName;
                string? rarity = null;
                if (entity.TryGetComponent<Mods>(out var mods))
                {
                    rarity = mods.ItemRarity.ToString();
                    if (!string.IsNullOrEmpty(mods.UniqueName))
                    {
                        display = mods.UniqueName;
                        groupKey = mods.UniqueName;
                    }
                }

                snapshot.Items.Add(new ItemSnapshot
                {
                    DisplayName = display,
                    GroupKey = groupKey,
                    StackSize = stackSize,
                    UnitValueEx = stackSize > 0 ? stackValueEx / stackSize : stackValueEx,
                    Rarity = rarity,
                    Category = baseType?.ClassName,
                });
            }
            catch (Exception ex)
            {
                _logError($"error reading stash item: {ex.Message}");
            }
        }

        return snapshot;
    }
}
```

- [ ] **Step 2: Verify the plugin builds**

Run:
```bash
cd ~/Work/My/StashValueTracker
exileCore2Package="$HOME/Work/My/StashValueTracker/lib" dotnet build StashValueTracker.csproj -c Debug 2>&1 | tail -8
```
Expected: `Build succeeded.` Resolve any member/`using` mismatch against the actual API (`NormalInventoryItem.Item`, `Inventory.VisibleInventoryItems`, `Inventory.InvType`, `StashElement.VisibleStashIndex`, `StashElement.TabName`, `Mods.UniqueName`, `BaseItemType.BaseName/ClassName`) and rebuild.

- [ ] **Step 3: Commit**

```bash
git add Scanning/StashScanner.cs
git commit -m "$(printf 'feat: add stash tab scanner\n\nCo-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>')"
```

---

### Task 9: Value window (ImGui UI)

**Files:**
- Create: `UI/ValueWindow.cs`

> ExileCore-dependent — gate is a clean build + in-game verification later.
> Pattern from `ExileMaps.Waypoints.cs:227-250`.

- [ ] **Step 1: Implement**

`UI/ValueWindow.cs`:
```csharp
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
    private readonly HashSet<int> _selectedTabs = new();
    private bool _initializedSelection;

    /// <summary>Renders the window. Sets <paramref name="showWindow"/> to false when the user closes it.</summary>
    public void Draw(SnapshotStore store, double divinePerExalted, bool bridgeAvailable, bool pricesReady, ref bool showWindow)
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

        // Default selection: all tabs selected the first time.
        if (!_initializedSelection && tabs.Count > 0)
        {
            foreach (var t in tabs) _selectedTabs.Add(t.Key);
            _initializedSelection = true;
        }

        var result = StashAggregator.Aggregate(tabs, _selectedTabs);

        ImGui.Text($"Total (selected): {CurrencyFormat.ExWithDiv(result.GrandTotalEx, divinePerExalted)}");
        ImGui.SameLine();
        ImGui.TextDisabled($"   |   {result.UnpricedCount} items unpriced");
        ImGui.Separator();

        DrawTabFilterPanel(store, tabs, divinePerExalted);
        ImGui.SameLine();
        DrawSummaryTable(result, divinePerExalted);

        ImGui.End();
    }

    private void DrawTabFilterPanel(SnapshotStore store, IReadOnlyList<TabSnapshot> tabs, double divinePerExalted)
    {
        ImGui.BeginChild("tabs", new System.Numerics.Vector2(220, 0), ImGuiChildFlags.Border);
        ImGui.TextDisabled("Tabs");
        ImGui.Separator();

        foreach (var tab in tabs.OrderBy(t => t.Key).ToList())
        {
            ImGui.PushID(tab.Key);

            var selected = _selectedTabs.Contains(tab.Key);
            if (ImGui.Checkbox($"{tab.Name}##sel", ref selected))
            {
                if (selected) _selectedTabs.Add(tab.Key);
                else _selectedTabs.Remove(tab.Key);
            }

            var tabTotal = tab.Items.Where(i => i.UnitValueEx > 0).Sum(i => i.UnitValueEx * i.StackSize);
            ImGui.TextDisabled($"  {CurrencyFormat.ExWithDiv(tabTotal, divinePerExalted)} · {Ago(tab.LastScannedUtc)}");

            ImGui.SameLine();
            if (ImGui.SmallButton("Forget"))
            {
                store.ForgetTab(tab.Key);
                store.Save();
                _selectedTabs.Remove(tab.Key);
            }

            ImGui.PopID();
        }

        ImGui.EndChild();
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
            ImGui.TableSetupColumn("Total", ImGuiTableColumnFlags.WidthFixed, 150 | (int)ImGuiTableColumnFlags.DefaultSort);
            ImGui.TableHeadersRow();

            var rows = SortRows(result.Rows);
            foreach (var row in rows)
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

    // Applies ImGui's current sort spec to the rows. Defaults to Total desc (handled by aggregator).
    private static List<AggregatedRow> SortRows(IReadOnlyList<AggregatedRow> rows)
    {
        var list = rows.ToList();
        var specsPtr = ImGui.TableGetSortSpecs();
        if (specsPtr.SpecsCount == 0) return list;

        var spec = specsPtr.Specs;
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
```

- [ ] **Step 2: Verify the plugin builds**

Run:
```bash
cd ~/Work/My/StashValueTracker
exileCore2Package="$HOME/Work/My/StashValueTracker/lib" dotnet build StashValueTracker.csproj -c Debug 2>&1 | tail -10
```
Expected: `Build succeeded.` If the ImGui.NET version exposes `BeginChild`/`ImGuiChildFlags`/`TableGetSortSpecs` differently, adapt the calls to the referenced ImGui.NET 1.90.0.1 surface (e.g. `BeginChild(string, Vector2, bool)`), keeping behavior identical.

- [ ] **Step 3: Commit**

```bash
git add UI/ValueWindow.cs
git commit -m "$(printf 'feat: add aggregated value window\n\nCo-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>')"
```

---

### Task 10: Plugin orchestration

**Files:**
- Create: `StashValueTracker.cs`

> ExileCore-dependent — gate is a clean build + in-game verification.

- [ ] **Step 1: Implement**

`StashValueTracker.cs`:
```csharp
using System;
using System.IO;
using ExileCore2;
using StashValueTracker.Pricing;
using StashValueTracker.Scanning;
using StashValueTracker.Storage;
using StashValueTracker.UI;

namespace StashValueTracker;

public class StashValueTracker : BaseSettingsPlugin<Settings>
{
    private NinjaPricerBridge _bridge = null!;
    private StashScanner _scanner = null!;
    private SnapshotStore _store = null!;
    private ValueWindow _window = null!;

    private string _currentLeague = "Standard";
    private int? _pendingTabIndex;
    private DateTime _pendingSince = DateTime.MinValue;
    private bool _scannedThisOpen;

    public override bool Initialise()
    {
        _bridge = new NinjaPricerBridge(GameController, msg => LogError($"[bridge] {msg}"));
        _scanner = new StashScanner(GameController, _bridge, msg => LogError($"[scan] {msg}"));
        _store = new SnapshotStore(Path.Combine(DirectoryFullName, "data"), msg => LogError($"[store] {msg}"));
        _window = new ValueWindow();

        _currentLeague = ResolveLeague();
        _store.LoadLeague(_currentLeague);
        return true;
    }

    public override void Tick()
    {
        if (!Settings.Enable) return;

        // Reload snapshots when the league changes.
        var league = ResolveLeague();
        if (!string.Equals(league, _currentLeague, StringComparison.OrdinalIgnoreCase))
        {
            _currentLeague = league;
            _store.LoadLeague(_currentLeague);
        }

        var stash = _scanner.GetVisibleStash();
        if (stash == null)
        {
            // Stash closed — reset so re-opening the same tab triggers a re-scan.
            _pendingTabIndex = null;
            return;
        }

        var index = _scanner.CurrentTabIndex();
        if (index == null) return;

        if (index != _pendingTabIndex)
        {
            _pendingTabIndex = index;
            _pendingSince = DateTime.Now;
            _scannedThisOpen = false;
            return;
        }

        if (_scannedThisOpen) return;
        if ((DateTime.Now - _pendingSince).TotalMilliseconds < Settings.ScanDebounceMs.Value) return;
        if (!_bridge.PricesReady) return;

        var snapshot = _scanner.ScanCurrentTab(DateTime.UtcNow);
        if (snapshot != null)
        {
            _store.UpsertTab(snapshot);
            _store.Save();
            _scannedThisOpen = true;
            LogMessage($"Scanned tab '{snapshot.Name}': {snapshot.Items.Count} items.");
        }
    }

    public override void Render()
    {
        if (!Settings.Enable) return;
        if (!Settings.ShowWindow) return;

        var show = true;
        _window.Draw(_store, _bridge.DivinePerExalted, _bridge.IsAvailable, _bridge.PricesReady, ref show);
        if (!show) Settings.ShowWindow.Value = false;
    }

    private string ResolveLeague()
    {
        var raw = GameController?.IngameState?.ServerData?.League;
        return string.IsNullOrWhiteSpace(raw) ? "Standard" : raw;
    }
}
```

- [ ] **Step 2: Verify the plugin builds**

Run:
```bash
cd ~/Work/My/StashValueTracker
exileCore2Package="$HOME/Work/My/StashValueTracker/lib" dotnet build StashValueTracker.csproj -c Debug 2>&1 | tail -10
```
Expected: `Build succeeded.` If `BaseSettingsPlugin`/`DirectoryFullName`/`LogMessage`/`LogError`/`ServerData.League` differ, align with the actual ExileCore2 base API (cross-checked against EssenceHelper) and rebuild.

- [ ] **Step 3: Commit**

```bash
git add StashValueTracker.cs
git commit -m "$(printf 'feat: wire up plugin orchestration and scan lifecycle\n\nCo-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>')"
```

---

### Task 11: Final verification, docs, and manual test checklist

**Files:**
- Modify: `README.md`

- [ ] **Step 1: Full build + full test run**

Run:
```bash
cd ~/Work/My/StashValueTracker
dotnet test tests/StashValueTracker.Tests/StashValueTracker.Tests.csproj 2>&1 | tail -8
exileCore2Package="$HOME/Work/My/StashValueTracker/lib" dotnet build StashValueTracker.csproj -c Debug 2>&1 | tail -5
```
Expected: all tests PASS; `Build succeeded.`

- [ ] **Step 2: Update README status**

In `README.md`, change the "Status" section from "🚧 In development" to note v1 is implemented and pending in-game verification, and add a "Usage" subsection: enable the plugin, open stash tabs to scan them, toggle "Show value window" in settings to view the aggregated total.

- [ ] **Step 3: Write the in-game manual test checklist into README**

Append a "Manual test checklist (Windows / in-game)" section to `README.md`:
```markdown
## Manual test checklist (Windows / in-game)

- [ ] With NinjaPricer loaded and price data ready, open a Currency tab → items get scanned (log line appears).
- [ ] Toggle "Show value window" → window shows the tab in the left panel and items on the right.
- [ ] Open a second tab → it appears; totals update; an item present in both tabs merges into one row with a "Tab +N" label and a tooltip listing both tabs.
- [ ] Re-open a tab after adding/removing items → it re-scans (LastScanned updates).
- [ ] Uncheck a tab in the filter → its items/total drop out of the summary.
- [ ] "Forget" a tab → it disappears and the data file updates.
- [ ] Restart the overlay → snapshots reload from disk; totals are present before re-opening tabs.
- [ ] Disable NinjaPricer → window shows the "not loaded" banner; no scanning occurs.
- [ ] Switch league → window reflects the new league's (separate) snapshots.
```

- [ ] **Step 4: Commit**

```bash
cd ~/Work/My/StashValueTracker
git add README.md
git commit -m "$(printf 'docs: mark v1 implemented and add manual test checklist\n\nCo-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>')"
```

- [ ] **Step 5: Final code review + finish the branch**

Dispatch a final code-reviewer over the whole branch diff, then use **superpowers:finishing-a-development-branch** to merge/PR. Unit tests must pass first (Step 1).

---

## Notes for the implementer

- **Linux build gate:** every ExileCore-dependent task ends with a `dotnet build` of the plugin
  assembly. Member/namespace mismatches against the compiled core are the most likely failure;
  resolve them against the live `ExileCore2.dll` and the reference plugins, keeping behavior identical.
- **Never commit `lib/`** — it is gitignored and contains proprietary DLLs.
- **No WinForms** — do not introduce `System.Windows.Forms` (e.g. `HotkeyNode`); it breaks the
  Linux build. The window is toggled by the `ShowWindow` setting.
- **Runtime testing is in-game on Windows** — the plugin assembly builds on Linux but does not run there.
```
