# Quickstart: Optimizer-to-Planner Export Integration

**Branch**: `005-optimizer-planner-export`

## Prerequisites

- .NET 8 SDK
- Solution builds: `dotnet build EveMarketAnalysis.sln`
- Tests pass: `dotnet test EveMarketAnalysisClient.Tests`

## Files to Create

| File | Purpose |
|------|---------|
| `EveMarketAnalysisClient/Models/ProductionBatchExport.cs` | Immutable record defining the JSON export payload contract |
| `EveMarketAnalysisClient.Tests/Models/ProductionBatchExportTests.cs` | Unit tests: serialization round-trip, runs clamping, merge logic |

## Files to Modify

| File | Change |
|------|--------|
| `EveMarketAnalysisClient/Pages/PortfolioOptimizer.cshtml` | Add checkbox column to rankings table, add "Export to Planner" button, add JS export logic |
| `EveMarketAnalysisClient/Pages/ProductionPlanner.cshtml` | Add JS import logic: read localStorage on blueprint load, auto-select, merge, show notification, clear key |

## Implementation Order (TDD)

1. **Red**: Write `ProductionBatchExportTests.cs` — serialization round-trip, runs clamping to 1, merge logic (import-wins)
2. **Green**: Create `ProductionBatchExport.cs` — immutable records with `System.Text.Json` serialization
3. **Refactor**: Verify tests pass, clean up
4. **Optimizer JS**: Add checkbox column, export button, payload creation, localStorage write, new tab navigation
5. **Planner JS**: Add localStorage read after blueprint fetch, auto-select loop, merge logic, notification banner, key removal
6. **Integration test**: Manual end-to-end verification — analyze in optimizer, select, export, verify planner imports correctly

## Key Patterns to Follow

- **Immutable records**: `record ProductionBatchExport(ImmutableArray<BlueprintRunExport> Items)`
- **Pure functions**: Static helper methods for payload creation and merge (no side effects)
- **Existing JS patterns**: Use `document.querySelectorAll()` with `data-typeid` attributes (matches planner pattern)
- **Existing localStorage pattern**: Try-catch wrapper (matches optimizer's `loadConfig()` / `saveConfig()` pattern)
- **Test pattern**: xUnit + FluentAssertions + `System.Text.Json.JsonSerializer` for round-trip tests

## Verification

```bash
# Build
dotnet build EveMarketAnalysis.sln

# Run tests
dotnet test EveMarketAnalysisClient.Tests

# Manual verification
dotnet run --project EveMarketAnalysisClient --launch-profile https
# 1. Navigate to Portfolio Optimizer, run analysis
# 2. Check several blueprints, click "Export to Planner"
# 3. Verify new tab opens with blueprints auto-selected and runs set
# 4. Verify notification banner appears and auto-dismisses
# 5. Generate shopping list to confirm imported selections work
```
