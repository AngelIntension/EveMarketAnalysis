# Implementation Plan: Material Shopping List Planner

**Branch**: `003-material-shopping-planner` | **Date**: 2026-03-15 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/003-material-shopping-planner/spec.md`

## Summary

Add a protected `/productionplanner` page where authenticated industrialists can multi-select owned blueprints, set per-blueprint run counts and "Produce Components" toggles, then generate an aggregated shopping list of materials to purchase. The recursive expansion engine substitutes intermediate materials with sub-materials when the character owns component blueprints, applying each blueprint's own ME. A separate procurement cost panel fetches lowest-sell-order prices from a selected trade hub region without recalculating the material list. Reuses existing `BlueprintDataService` (embedded SDE data), `EsiMarketClient`, `EsiCharacterClient`, and `TradeHubRegion` model. Follows the skeleton-loading + JSON handler pattern established by `ManufacturingProfitability` page.

## Technical Context

**Language/Version**: C# / .NET 8.0 (nullable reference types, implicit usings)
**Primary Dependencies**: ASP.NET Core 8 Razor Pages, Kiota-generated ESI client, `System.Collections.Immutable`
**Storage**: `IMemoryCache` (5-min market data, 24h name/volume resolution); embedded `blueprints.json` (SDE) via `BlueprintDataService`
**Testing**: xUnit.net v2+, FluentAssertions, Moq, AutoFixture (AutoMoqCustomization)
**Target Platform**: Windows/Linux server (Kestrel), browser client
**Project Type**: ASP.NET Core Razor Pages web application
**Performance Goals**: Shopping list generation <5s for 20 blueprints; cost refresh <3s on region change
**Constraints**: All new code must be functional/immutable style (constitution Principle I); TDD mandatory (constitution Principle III); must stay within existing 2-project solution structure
**Scale/Scope**: Single user per session; up to 20 simultaneous blueprint selections; 5 trade hub regions

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| **I. Functional & Immutable Style** | PASS | All new models as records with `ImmutableArray`. Pure recursive expansion function. `with`-expressions for derived copies. No mutable state except Blazor lifecycle boundaries. |
| **II. Sensitive Data Handling** | PASS | No new secrets introduced. Feature reuses existing ESI OAuth tokens managed by `EsiTokenService`. No new credentials or API keys. |
| **III. Test-Driven Development** | PASS | TDD workflow enforced: tests listed before implementation in every task. Unit tests for recursive expansion, ME calculations, aggregation. Integration tests for market cost injection and cross-service data flow. |
| **Rate-Limit Compliance** | PASS | Market calls go through existing `EsiRateLimitHandler`. Concurrency limited via `SemaphoreSlim` (pattern from `ProfitabilityCalculator`). |
| **Caching** | PASS | Blueprint data cached on page load. Market snapshots: 5-min TTL via `IMemoryCache`. Name/volume resolution: 24h cache. |
| **Parallel Dispatch** | PASS | Independent market calls dispatched via `Task.WhenAll` with semaphore throttle. |
| **Bulk Endpoints First** | PASS | Type name resolution via bulk `POST /universe/names` (existing pattern). |
| **Cache Immutability** | PASS | All cached structures are immutable records/arrays. No in-place mutation. |
| **Solution Structure** | PASS | All code within `EveMarketAnalysisClient/` and `EveMarketAnalysisClient.Tests/`. No new projects. |

**Pre-Phase 0 Gate: PASSED** — No violations detected.

## Project Structure

### Documentation (this feature)

```text
specs/003-material-shopping-planner/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output (JSON handler contracts)
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
EveMarketAnalysisClient/
├── Models/
│   ├── BlueprintSelection.cs          # User's blueprint + runs + toggle
│   ├── MaterialTreeNode.cs            # Recursive material expansion tree
│   ├── ShoppingListItem.cs            # Aggregated material row
│   └── ShoppingListResponse.cs        # JSON response envelope
├── Services/
│   ├── Interfaces/
│   │   └── IShoppingListService.cs    # Shopping list orchestrator interface
│   └── ShoppingListService.cs         # Orchestrates expansion + aggregation + costs
├── Pages/
│   ├── ProductionPlanner.cshtml       # Razor view with skeleton loading
│   └── ProductionPlanner.cshtml.cs    # Page model with JSON handlers
└── (existing files reused: BlueprintDataService, EsiMarketClient, EsiCharacterClient, TradeHubRegion)

EveMarketAnalysisClient.Tests/
├── Services/
│   └── ShoppingListServiceTests.cs    # Unit + integration tests
├── Pages/
│   └── ProductionPlannerTests.cs      # Page handler tests
└── Models/
    └── MaterialTreeNodeTests.cs       # Pure function tests for recursive expansion
```

**Structure Decision**: Follows the existing single-project layout. New models go in `Models/`, new service in `Services/` with interface in `Services/Interfaces/`, new page in `Pages/`. Reuses existing `BlueprintDataService` (singleton), `EsiMarketClient` (scoped), and `EsiCharacterClient` (scoped). No new projects or solution restructuring needed.

## Complexity Tracking

> No constitution violations detected. No complexity justifications needed.
