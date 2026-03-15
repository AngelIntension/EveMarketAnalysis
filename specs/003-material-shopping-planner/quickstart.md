# Quickstart: Material Shopping List Planner

**Branch**: `003-material-shopping-planner` | **Date**: 2026-03-15

## Prerequisites

- .NET 8 SDK
- ESI OAuth credentials configured via `dotnet user-secrets` (existing setup)
- Authenticated EVE character with blueprints

## Build & Run

```bash
# Build
dotnet build EveMarketAnalysis.sln

# Run (must use https profile)
dotnet run --project EveMarketAnalysisClient --launch-profile https

# Navigate to https://localhost:7272/productionplanner (requires login)
```

## Run Tests

```bash
# All tests
dotnet test EveMarketAnalysisClient.Tests

# Shopping list tests only
dotnet test EveMarketAnalysisClient.Tests --filter "FullyQualifiedName~ShoppingList"

# Material tree tests only
dotnet test EveMarketAnalysisClient.Tests --filter "FullyQualifiedName~MaterialTreeNode"
```

## Key Files

| Purpose | File |
|---------|------|
| Page model | `EveMarketAnalysisClient/Pages/ProductionPlanner.cshtml.cs` |
| Page view | `EveMarketAnalysisClient/Pages/ProductionPlanner.cshtml` |
| Shopping list service | `EveMarketAnalysisClient/Services/ShoppingListService.cs` |
| Service interface | `EveMarketAnalysisClient/Services/Interfaces/IShoppingListService.cs` |
| Blueprint selection model | `EveMarketAnalysisClient/Models/BlueprintSelection.cs` |
| Material tree node model | `EveMarketAnalysisClient/Models/MaterialTreeNode.cs` |
| Shopping list item model | `EveMarketAnalysisClient/Models/ShoppingListItem.cs` |
| Shopping list response model | `EveMarketAnalysisClient/Models/ShoppingListResponse.cs` |
| Service tests | `EveMarketAnalysisClient.Tests/Services/ShoppingListServiceTests.cs` |
| Page tests | `EveMarketAnalysisClient.Tests/Pages/ProductionPlannerTests.cs` |
| Model tests | `EveMarketAnalysisClient.Tests/Models/MaterialTreeNodeTests.cs` |

## Existing Dependencies (reused, not modified)

| Service | Registration | Purpose |
|---------|-------------|---------|
| `IBlueprintDataService` | Singleton | SDE blueprint data (embedded `blueprints.json`) |
| `IEsiMarketClient` | Scoped | Market snapshot fetching with 5-min cache |
| `IEsiCharacterClient` | Scoped | Character blueprint fetching with bulk name resolution |
| `TradeHubRegion` | Static model | 5 hardcoded trade hub regions |

## DI Registration (add to Program.cs)

```csharp
builder.Services.AddScoped<IShoppingListService, ShoppingListService>();
```

## Architecture Notes

- **Skeleton loading**: Page renders with placeholders; JS calls `?handler=Blueprints` on load, then `?handler=ShoppingList` on generate, then `?handler=Costs` on region select.
- **Separation of concerns**: List generation and cost fetching are independent handlers. Region change calls only the cost handler.
- **Recursive expansion**: Pure function in `ShoppingListService` — takes immutable inputs, returns immutable `MaterialTreeNode` tree. Cycle detection via `ImmutableHashSet<int>` of visited type IDs.
- **Owned blueprint map**: `FrozenDictionary<int, CharacterBlueprint>` keyed by `ProducedTypeId`. Built once per request. When multiple blueprints produce the same item, the one with highest ME wins.
