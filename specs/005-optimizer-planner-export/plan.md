# Implementation Plan: Optimizer-to-Planner Export Integration

**Branch**: `005-optimizer-planner-export` | **Date**: 2026-03-16 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/005-optimizer-planner-export/spec.md`

## Summary

Add a checkbox selection column to the Portfolio Optimizer rankings table with an "Export to Planner" button that serializes selected blueprint type IDs, names, and run counts to localStorage. The Production Planner detects this payload on page load, auto-selects matching blueprints with imported run counts, merges with any existing selections, shows a notification, and clears the key. Implementation is primarily client-side JavaScript with a single new C# model defining the JSON contract and C# unit tests for serialization round-trip and merge logic.

## Technical Context

**Language/Version**: C# 12 / .NET 8, JavaScript (vanilla, no framework)
**Primary Dependencies**: ASP.NET Core 8 Razor Pages, xUnit, FluentAssertions, Moq, AutoFixture
**Storage**: Browser localStorage (key: `pendingProductionBatch`), no server-side storage changes
**Testing**: xUnit with FluentAssertions, Moq, AutoFixture (C# model/serialization tests); manual verification for JS behavior
**Target Platform**: Modern browsers (same as existing app)
**Project Type**: Web application (ASP.NET Core 8 Razor Pages)
**Performance Goals**: N/A — purely client-side, no new server calls
**Constraints**: No new NuGet packages, no new server endpoints, stays within existing two-project solution structure
**Scale/Scope**: Touches 2 Razor pages (JS sections), adds 1 new C# model file, adds 1 new C# test file

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Evidence |
|-----------|--------|----------|
| I. Functional & Immutable Style | PASS | Export payload model is an immutable `record` with `ImmutableArray`. JS payload creation uses pure functions that build new objects without mutating table state. |
| II. Sensitive Data Handling | PASS | No secrets involved. localStorage stores only blueprint type IDs, names, and run counts — all public game data. |
| III. Test-Driven Development | PASS | C# tests for `ProductionBatchExport` serialization round-trip, zero-runs clamping, import merge logic. JS changes follow existing untested JS pattern (project has no JS test framework). |
| Rate-Limit Compliance | PASS | No new ESI calls. |
| Caching | PASS | No cache changes. |
| Parallel Dispatch | N/A | No async operations added. |
| Bulk Endpoints First | N/A | No ESI lookups added. |
| Cache Immutability | PASS | No cache mutations. |
| Solution Structure | PASS | All changes within existing `EveMarketAnalysisClient` and `EveMarketAnalysisClient.Tests` projects. |

**Pre-design gate: PASSED. No violations.**

## Project Structure

### Documentation (this feature)

```text
specs/005-optimizer-planner-export/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output
│   └── localstorage-contract.md
└── tasks.md             # Phase 2 output (/speckit.tasks)
```

### Source Code (repository root)

```text
EveMarketAnalysisClient/
├── Models/
│   └── ProductionBatchExport.cs       # NEW: immutable export payload record
├── Pages/
│   ├── PortfolioOptimizer.cshtml      # MODIFIED: add checkbox column + export button + JS export logic
│   └── ProductionPlanner.cshtml       # MODIFIED: add JS import logic (localStorage detection, auto-select, merge, notification)

EveMarketAnalysisClient.Tests/
├── Models/
│   └── ProductionBatchExportTests.cs  # NEW: serialization round-trip, zero-runs clamping, merge logic tests
```

**Structure Decision**: All changes fit within the existing ASP.NET Core Razor Pages project. No new projects, services, or server-side endpoints needed. The feature is implemented as client-side JavaScript with a shared C# model defining the JSON contract for type safety and testability.

## Complexity Tracking

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| JS logic lacks automated unit tests (Constitution III: 100% coverage) | Project has no JS test framework. Feature is primarily client-side JS (export, import, merge, notification). C# model mirrors the JS contract and is fully tested for serialization, clamping, and merge logic. | Adding a JS test framework (Jest, Vitest) would introduce a new toolchain dependency for ~4 pure JS functions, violating the "no new dependencies" constraint. Manual verification via quickstart.md covers the JS paths. C# tests validate the contract shape and business logic that the JS mirrors. |
