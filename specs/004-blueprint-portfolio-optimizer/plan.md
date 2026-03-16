# Implementation Plan: Blueprint Portfolio Optimizer

**Branch**: `004-blueprint-portfolio-optimizer` | **Date**: 2026-03-15 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/004-blueprint-portfolio-optimizer/spec.md`

## Summary

Add a `/portfoliooptimizer` page that ranks owned blueprints by realistic ISK/hr (including system cost index, broker fees, and sales tax), organizes progression through five production phases, recommends BPO purchases and research targets, and provides configurable thresholds with an explicit "Refresh Analysis" workflow. All controls persist in browser local storage; computation triggers only on button click.

## Technical Context

**Language/Version**: C# / .NET 8.0
**Primary Dependencies**: ASP.NET Core 8 Razor Pages, Kiota-generated ESI client, Microsoft.Extensions.Caching.Memory
**Storage**: Browser local storage (user configuration); IMemoryCache (market data, 5-min sliding)
**Testing**: xUnit.net v2+, FluentAssertions, Moq, AutoFixture (AutoMoqCustomization)
**Target Platform**: Windows/Linux web server (self-hosted Kestrel)
**Project Type**: Web application (ASP.NET Core Razor Pages)
**Performance Goals**: Full refresh ≤ 10s for ≤ 200 blueprints; 5-min market cache
**Constraints**: 20 concurrent ESI calls max; ESI rate-limit compliance; soft cap 200–300 blueprints per refresh
**Scale/Scope**: Single authenticated user, ~50–300 owned blueprints, 5 trade hub stations

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| I. Functional & Immutable Style | PASS | All new models are immutable records with ImmutableArray/FrozenDictionary collections. Pure functions for phase scoring, ISK/hr calculation, and recommendation logic. |
| II. Sensitive Data Handling | PASS | No new secrets. Reuses existing ESI OAuth flow. Local storage stores only UI preferences (station IDs, fee percentages). |
| III. Test-Driven Development | PASS | TDD workflow enforced. Tests listed before implementation in task ordering. Covers phase triggers, ISK/hr calculation, recommendation filtering, edge cases. |
| Rate-Limit Compliance | PASS | Reuses existing EsiRateLimitHandler. SemaphoreSlim(20) for concurrent calls. FR-021 adds user-facing retry message. |
| Caching | PASS | 5-min sliding cache for market data (FR-019). 24-hr cache for type names/volumes (existing pattern). |
| Parallel Dispatch | PASS | Task.WhenAll for independent market fetches. Matches existing ProfitabilityCalculator pattern. |
| Bulk Endpoints First | PASS | POST /universe/names for bulk name resolution (existing pattern). |
| Cache Immutability | PASS | All cached structures are immutable records/arrays. Never mutated after storage. |
| Solution Structure | PASS | All code stays within EveMarketAnalysisClient/ and EveMarketAnalysisClient.Tests/. No new projects. |

## Project Structure

### Documentation (this feature)

```text
specs/004-blueprint-portfolio-optimizer/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   └── portfolio-api.md # AJAX endpoint contracts
└── tasks.md             # Phase 2 output (created by /speckit.tasks)
```

### Source Code (repository root)

```text
EveMarketAnalysisClient/
├── Data/
│   ├── phases.json                          # NEW: static phase definitions with type IDs
│   └── skill-requirements.json              # NEW: blueprint → required skills mapping
├── Models/
│   ├── PortfolioAnalysis.cs                 # NEW: top-level analysis result record
│   ├── BlueprintRankingEntry.cs             # NEW: enriched blueprint with ISK/hr
│   ├── BpoPurchaseRecommendation.cs         # NEW: unowned BPO recommendation
│   ├── ResearchRecommendation.cs            # NEW: under-researched BP recommendation
│   ├── PhaseDefinition.cs                   # NEW: static phase data record
│   ├── PhaseStatus.cs                       # NEW: phase with completion status
│   ├── PortfolioConfiguration.cs            # NEW: user-configurable thresholds
│   └── TradeHubRegion.cs                    # EXISTING (unchanged)
├── Services/
│   ├── Interfaces/
│   │   ├── IPortfolioAnalyzer.cs            # NEW: orchestrates full analysis
│   │   ├── IPhaseService.cs                 # NEW: phase definitions & completion logic
│   │   └── IEsiMarketClient.cs              # EXISTING (extended with GetRegionMarketSnapshotAsync)
│   ├── PortfolioAnalyzer.cs                 # NEW: implementation
│   ├── PhaseService.cs                      # NEW: loads phases.json, evaluates completion
│   └── ProfitabilityCalculator.cs           # EXISTING (unchanged — reuse CalculateAsync)
├── Pages/
│   ├── PortfolioOptimizer.cshtml            # NEW: Razor page with EVE dark theme
│   └── PortfolioOptimizer.cshtml.cs         # NEW: page model with AJAX handlers

EveMarketAnalysisClient.Tests/
├── Unit/
│   ├── PhaseServiceTests.cs                 # NEW: phase loading, GetAllPhases, GetPhaseForTypeId
│   ├── EsiMarketClientRegionTests.cs        # NEW: GetRegionMarketSnapshotAsync tests
│   ├── PortfolioAnalyzerTests.cs            # NEW: ISK/hr calc, ranking, skill gating, what-if
│   ├── PortfolioConfigurationTests.cs       # NEW: parameter validation bounds
│   ├── PhaseCompletionTests.cs              # NEW: slot-based + income triggers, manual override
│   ├── BpoRecommendationTests.cs            # NEW: phase scoping, NPC prices, ROI/payback
│   ├── ResearchRecommendationTests.cs       # NEW: ME/TE improvement ranking
│   ├── SimulationTests.cs                   # NEW: simulate next phase, threshold effects
│   └── PortfolioEdgeCaseTests.cs            # NEW: zero BPs, missing data, rate limits
├── Pages/
│   └── PortfolioOptimizerPageTests.cs       # NEW: page handler tests
└── Services/
    └── PortfolioAnalyzerIntegrationTests.cs # NEW: cross-service data flow
```

**Structure Decision**: Follows existing project layout. New models in Models/, new services in Services/, new page in Pages/. No new projects — all within existing EveMarketAnalysisClient/ and EveMarketAnalysisClient.Tests/.

## Complexity Tracking

No constitution violations. No complexity justifications needed.
