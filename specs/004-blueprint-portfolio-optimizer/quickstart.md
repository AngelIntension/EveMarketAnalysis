# Quickstart: Blueprint Portfolio Optimizer

**Branch**: `004-blueprint-portfolio-optimizer` | **Date**: 2026-03-15

## Prerequisites

- .NET 8.0 SDK
- ESI OAuth credentials configured via `dotnet user-secrets`
- Authenticated EVE Online character with owned blueprints

## Build & Run

```bash
dotnet build EveMarketAnalysis.sln
dotnet run --project EveMarketAnalysisClient --launch-profile https
```

Navigate to `https://localhost:7272/PortfolioOptimizer` (requires login).

## Run Tests

```bash
dotnet test EveMarketAnalysisClient.Tests
```

## Key Files

| File | Purpose |
|------|---------|
| `EveMarketAnalysisClient/Data/phases.json` | Static phase definitions with type IDs |
| `EveMarketAnalysisClient/Services/PortfolioAnalyzer.cs` | Core analysis orchestrator |
| `EveMarketAnalysisClient/Services/PhaseService.cs` | Phase loading and completion logic |
| `EveMarketAnalysisClient/Models/PortfolioAnalysis.cs` | Top-level result record |
| `EveMarketAnalysisClient/Pages/PortfolioOptimizer.cshtml` | Page UI |
| `EveMarketAnalysisClient/Pages/PortfolioOptimizer.cshtml.cs` | AJAX handler |

## Architecture

```
Browser (local storage) ──config──> JS ──AJAX──> PortfolioOptimizer.cshtml.cs
                                                        │
                                                        ▼
                                                PortfolioAnalyzer
                                               /        |        \
                                      PhaseService  EsiMarketClient  EsiCharacterClient
                                      (phases.json)  (market data)   (blueprints, skills)
                                              │
                                              ▼
                                      BlueprintDataService
                                      (blueprints.json)
```

## Configuration Defaults

All persisted in browser local storage (`portfolioConfig` key):

| Setting | Default | Range |
|---------|---------|-------|
| Procurement Station | Jita 4-4 | Any trade hub |
| Selling Hub | Jita 4-4 | Any trade hub |
| Manufacturing System | Jita | Any system ID |
| Buying Broker Fee | 3.0% | 0–100% |
| Selling Broker Fee | 3.0% | 0–100% |
| Sales Tax | 3.6% | 0–100% |
| Min ISK/hr | 25,000,000 | ≥ 0 |
| Daily Income Goal | 750,000,000 | ≥ 0 |
| Manufacturing Slots | 11 | 1–50 |

## Testing Strategy

1. **Unit tests first** (TDD red-green-refactor):
   - PhaseService: loading, completion triggers (slot count, income fallback)
   - PortfolioAnalyzer: ISK/hr calculation with broker fees, ranking, filtering
   - Recommendation logic: phase scoping, research prioritization
2. **Integration tests**: Cross-service data flow with mocked ESI
3. **Page tests**: Handler validation, parameter parsing, error responses
