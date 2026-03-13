# Implementation Plan: Manufacturing Profitability Calculator

**Branch**: `002-manufacturing-profitability` | **Date**: 2026-03-13 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/002-manufacturing-profitability/spec.md`

## Summary

Add a Manufacturing Profitability page that fetches the authenticated character's blueprints from ESI, resolves manufacturing inputs/outputs from bundled SDE data, fetches live market prices from ESI for a user-selected trade hub region, calculates profitability (accounting for ME/TE, taxes, and fees), and displays a sortable top-50 table ranked by ISK/hour. Market data is cached for 5 minutes. The tax rate is user-adjustable (default 8%). Follows the existing skeleton loading pattern from CharacterSummary.

## Technical Context

**Language/Version**: C# / .NET 8.0
**Primary Dependencies**: ASP.NET Core 8 Razor Pages, Microsoft Kiota (ESI client), System.Collections.Immutable, System.Text.Json
**Storage**: IMemoryCache (in-process, no database), bundled JSON file for SDE data
**Testing**: xUnit 2.5.3, FluentAssertions 6.12.2, Moq 4.20.72, AutoFixture 4.18.1 (with AutoMoqCustomization)
**Target Platform**: Windows / Linux server (.NET 8 cross-platform)
**Project Type**: Web application (ASP.NET Core Razor Pages)
**Performance Goals**: Profitability table loads within 30 seconds for 100 blueprints; market data fetched in parallel with concurrency limiting
**Constraints**: ESI rate limit compliance (existing EsiRateLimitHandler), 5-minute market cache TTL, top 50 results displayed
**Scale/Scope**: Single user at a time (personal tool), up to ~1000 blueprints per character, ~200 unique market types

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### Pre-Research Gate (Phase 0)

| Principle | Status | Evidence |
|-----------|--------|----------|
| **I. Functional & Immutable Style** | PASS | All new models are immutable records with ImmutableArray collections. Profitability calculation is a pure function. |
| **II. Sensitive Data Handling** | PASS | No new secrets introduced. ESI OAuth tokens are already managed via existing EsiTokenService. Market endpoints are public (no auth needed). Blueprint scope added to existing EsiOptions config. |
| **III. Test-Driven Development** | PASS | Plan follows TDD: tests defined before implementation for every service and page. xUnit + FluentAssertions + Moq + AutoFixture stack. |
| **Parallel Dispatch** | PASS | Market data for all types fetched concurrently via Task.WhenAll with concurrency limiter. |
| **Bulk Endpoints First** | PASS | POST /universe/names used for bulk name resolution (existing pattern). Market orders fetched per-type (no bulk alternative exists in ESI). |
| **Cache Immutability** | PASS | Market snapshots are immutable records. No mutation of cached data. |
| **Purity of Wrappers** | PASS | EsiMarketClient accepts inputs, returns immutable results. ProfitabilityCalculator is a pure function. |
| **Solution Structure** | PASS | All code within existing EveMarketAnalysisClient project. No new projects added. Tests in existing EveMarketAnalysisClient.Tests project. |

### Post-Design Gate (Phase 1)

| Principle | Status | Evidence |
|-----------|--------|----------|
| **I. Functional & Immutable Style** | PASS | Data model uses records throughout. BlueprintActivity, MarketSnapshot, ProfitabilityResult all immutable. Collections use ImmutableArray. ProfitabilityCalculator takes inputs, returns new results — no side effects. |
| **II. Sensitive Data Handling** | PASS | No new secrets. Blueprint scope is an ESI permission, not a secret. |
| **III. Test-Driven Development** | PASS | Test files mapped for every new service and page handler. Unit tests cover pure calculation logic. Integration tests cover ESI client caching and pagination. |
| **Parallel Dispatch** | PASS | Research R7 defines parallel market fetch with SemaphoreSlim concurrency limiter. Blueprint fetch + market fetch are independent and dispatched concurrently. |
| **Bulk Endpoints First** | PASS | Bulk name resolution via POST /universe/names for all type IDs (materials + products). No per-ID name lookups. |
| **Cache Immutability** | PASS | MarketSnapshot is a record. Cached once, never mutated. New snapshots created on cache miss. |
| **Solution Structure** | PASS | No new projects. All files within existing project boundaries. |

## Project Structure

### Documentation (this feature)

```text
specs/002-manufacturing-profitability/
├── plan.md              # This file
├── spec.md              # Feature specification
├── research.md          # Phase 0 research decisions
├── data-model.md        # Entity definitions and relationships
├── quickstart.md        # Developer quickstart guide
├── contracts/
│   └── page-handlers.md # Page handler API contracts
├── checklists/
│   └── requirements.md  # Spec quality checklist
└── tasks.md             # Phase 2 output (created by /speckit.tasks)
```

### Source Code (repository root)

```text
EveMarketAnalysisClient/
├── Pages/
│   └── ManufacturingProfitability.cshtml(.cs)   # New Razor Page
├── Services/
│   ├── Interfaces/
│   │   ├── IEsiMarketClient.cs                  # New interface
│   │   ├── IBlueprintDataService.cs             # New interface
│   │   └── IProfitabilityCalculator.cs          # New interface
│   ├── EsiMarketClient.cs                       # New service
│   ├── BlueprintDataService.cs                  # New service
│   └── ProfitabilityCalculator.cs               # New service
├── Models/
│   ├── CharacterBlueprint.cs                    # New record
│   ├── BlueprintActivity.cs                     # New record
│   ├── MaterialRequirement.cs                   # New record
│   ├── MarketSnapshot.cs                        # New record
│   ├── TradeHubRegion.cs                        # New record
│   ├── ProfitabilitySettings.cs                 # New record
│   ├── ProfitabilityResult.cs                   # New record
│   └── ProfitabilityResponse.cs                 # New record
├── Data/
│   └── blueprints.json                          # Bundled SDE data
└── Program.cs                                   # Modified (DI registration)

EveMarketAnalysisClient/Services/EsiCharacterClient.cs  # Modified (add GetCharacterBlueprintsAsync)
EveMarketAnalysisClient/Services/Interfaces/IEsiCharacterClient.cs  # Modified (add method)

EveMarketAnalysisClient.Tests/
├── Unit/
│   ├── ProfitabilityCalculatorTests.cs          # New tests
│   ├── BlueprintDataServiceTests.cs             # New tests
│   └── TradeHubRegionTests.cs                   # New tests
├── Services/
│   ├── EsiMarketClientTests.cs                  # New tests
│   └── ProfitabilityIntegrationTests.cs         # New tests
└── Pages/
    └── ManufacturingProfitabilityPageTests.cs   # New tests
```

**Structure Decision**: All new code added within the existing two-project solution structure per the constitution's Solution Structure constraint. New services follow the established pattern of interface + implementation in `Services/` with DI registration in `Program.cs`. New models are immutable records in `Models/`. The SDE data file is placed in a new `Data/` directory as an embedded resource.

## Complexity Tracking

No constitution violations. All design decisions comply with existing constraints.
