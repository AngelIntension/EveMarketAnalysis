# Quickstart: Manufacturing Profitability Calculator

**Branch**: `002-manufacturing-profitability` | **Date**: 2026-03-13

## Prerequisites

- .NET 8 SDK
- EVE Online developer application with ESI scopes: `esi-characters.read_blueprints.v1`
- User secrets configured per existing setup (see `CLAUDE.md`)
- SDE blueprint data file (see below)

## New Files to Create

### Models (Records)

```
EveMarketAnalysisClient/Models/
├── CharacterBlueprint.cs        # Blueprint from ESI
├── BlueprintActivity.cs         # SDE manufacturing data
├── MaterialRequirement.cs       # Material with base + adjusted qty
├── MarketSnapshot.cs            # Market orders + history snapshot
├── TradeHubRegion.cs            # Static region definition
├── ProfitabilitySettings.cs     # User-adjustable settings
├── ProfitabilityResult.cs       # Computed profitability per blueprint
└── ProfitabilityResponse.cs     # JSON response wrapper
```

### Services

```
EveMarketAnalysisClient/Services/
├── Interfaces/
│   ├── IEsiMarketClient.cs      # Market orders + history fetching
│   ├── IBlueprintDataService.cs # SDE data access
│   └── IProfitabilityCalculator.cs  # Profit computation
├── EsiMarketClient.cs           # ESI market endpoint wrapper
├── BlueprintDataService.cs      # Bundled SDE JSON reader
└── ProfitabilityCalculator.cs   # Pure profit calculation logic
```

### Pages

```
EveMarketAnalysisClient/Pages/
└── ManufacturingProfitability.cshtml(.cs)  # Razor Page + handler
```

### Static Data

```
EveMarketAnalysisClient/Data/
└── blueprints.json              # Extracted SDE manufacturing data
```

### Tests

```
EveMarketAnalysisClient.Tests/
├── Unit/
│   ├── ProfitabilityCalculatorTests.cs  # ME/TE formulas, profit math
│   ├── BlueprintDataServiceTests.cs     # SDE data loading
│   └── TradeHubRegionTests.cs           # Static region validation
├── Services/
│   ├── EsiMarketClientTests.cs          # Market data fetching, caching
│   └── ProfitabilityCalculatorIntegrationTests.cs  # End-to-end calc
└── Pages/
    └── ManufacturingProfitabilityPageTests.cs  # Page handler tests
```

## Build & Run

```bash
# Build
dotnet build EveMarketAnalysis.sln

# Run tests
dotnet test EveMarketAnalysisClient.Tests

# Run the app
dotnet run --project EveMarketAnalysisClient --launch-profile https
# Navigate to https://localhost:7272/ManufacturingProfitability
```

## Key Implementation Notes

1. **Follow existing patterns**: Mirror `CharacterSummary` page for skeleton loading and async data fetch via named handler.
2. **Extend `EsiCharacterClient`**: Add `GetCharacterBlueprintsAsync` method alongside existing character endpoints.
3. **Register new services in `Program.cs`**: `IEsiMarketClient`, `IBlueprintDataService`, `IProfitabilityCalculator`.
4. **SDE data file**: Must be generated from EVE SDE `blueprints.yaml` before implementation. Include as embedded resource or `wwwroot/data/` static file.
5. **Constitution compliance**: All new records must be immutable. All independent ESI calls must use `Task.WhenAll`. Market data must be cached via `IMemoryCache`. Tests first (TDD).

## ESI Scopes Required

The existing OAuth flow needs the `esi-characters.read_blueprints.v1` scope added to the configured scopes in `EsiOptions`. Market endpoints are public and do not require authentication.
