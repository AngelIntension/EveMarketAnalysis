# Tasks: Blueprint Portfolio Optimizer

**Input**: Design documents from `/specs/004-blueprint-portfolio-optimizer/`
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, contracts/

**Tests**: Included per constitution (TDD is NON-NEGOTIABLE — Principle III).

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Static data files, model records, and service interfaces needed by all stories

- [x] T001 Create phases.json with type IDs for all 5 production phases in EveMarketAnalysisClient/Data/phases.json. Source type IDs from EVE SDE category/group IDs: Phase 1 = category 6 group 25 (Frigate) + small weapon/module/rig/ammo groups; Phase 2 = group 420 (Destroyer) + small modules/rigs; Phase 3 = groups 26,419 (Cruiser, Battlecruiser) + medium modules/rigs; Phase 4 = group 27 (Battleship) + large modules/rigs/ammo; Phase 5 = capitals + T2 entry items. Mark as embedded resource in .csproj (follow blueprints.json pattern).
- [x] T002 [P] Create PhaseDefinition record in EveMarketAnalysisClient/Models/PhaseDefinition.cs
- [x] T003 [P] Create PhaseStatus record in EveMarketAnalysisClient/Models/PhaseStatus.cs
- [x] T004 [P] Create PortfolioConfiguration record with defaults in EveMarketAnalysisClient/Models/PortfolioConfiguration.cs
- [x] T005 [P] Create BlueprintRankingEntry record in EveMarketAnalysisClient/Models/BlueprintRankingEntry.cs
- [x] T006 [P] Create BpoPurchaseRecommendation record in EveMarketAnalysisClient/Models/BpoPurchaseRecommendation.cs
- [x] T007 [P] Create ResearchRecommendation record in EveMarketAnalysisClient/Models/ResearchRecommendation.cs
- [x] T008 [P] Create PortfolioAnalysis top-level result record in EveMarketAnalysisClient/Models/PortfolioAnalysis.cs
- [x] T009 [P] Create IPhaseService interface in EveMarketAnalysisClient/Services/Interfaces/IPhaseService.cs
- [x] T010 [P] Create IPortfolioAnalyzer interface in EveMarketAnalysisClient/Services/Interfaces/IPortfolioAnalyzer.cs
- [x] T010a [P] Create skill-requirements.json mapping blueprint type IDs to required skill IDs + levels for manufacturing in EveMarketAnalysisClient/Data/skill-requirements.json. Mark as embedded resource in .csproj. Source from EVE SDE blueprints → manufacturing → skills.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core services that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

### Tests for Foundational

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [x] T011 [P] PhaseService unit tests: loading phases.json, GetAllPhases, GetPhaseForTypeId in EveMarketAnalysisClient.Tests/Unit/PhaseServiceTests.cs
- [x] T012 [P] EsiMarketClient unit tests for new GetRegionMarketSnapshotAsync method in EveMarketAnalysisClient.Tests/Unit/EsiMarketClientRegionTests.cs

### Implementation for Foundational

- [x] T013 Implement PhaseService: load phases.json as embedded resource, lazy singleton, GetAllPhases(), GetPhaseForTypeId() in EveMarketAnalysisClient/Services/PhaseService.cs
- [x] T014 Add GetRegionMarketSnapshotAsync to IEsiMarketClient and implement in EsiMarketClient (region-wide, no station filter) in EveMarketAnalysisClient/Services/EsiMarketClient.cs
- [x] T015 Register IPhaseService (singleton) and IPortfolioAnalyzer (scoped) in EveMarketAnalysisClient/Program.cs

**Checkpoint**: Foundation ready — PhaseService loads phase data, EsiMarketClient supports region-wide snapshots

---

## Phase 3: User Story 1 — Owned Blueprint ISK/hr Ranking (Priority: P1) 🎯 MVP

**Goal**: Sortable table of owned blueprints ranked by realistic ISK/hr with station-level pricing, broker fees, sales tax, system cost index, and ME/TE what-if sliders. "Refresh Analysis" triggers all computation.

**Independent Test**: Log in with a character owning blueprints, navigate to `/portfoliooptimizer`, click Refresh Analysis, verify ISK/hr values match manual calculations.

### Tests for User Story 1

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [x] T016 [P] [US1] PortfolioAnalyzer unit tests: ISK/hr calculation with broker fees, sales tax, system cost index, ME/TE adjustments in EveMarketAnalysisClient.Tests/Unit/PortfolioAnalyzerTests.cs
- [x] T017 [P] [US1] PortfolioAnalyzer unit tests: ranking sort order, skill gating exclusion, error handling for missing market data in EveMarketAnalysisClient.Tests/Unit/PortfolioAnalyzerTests.cs
- [x] T018 [P] [US1] PortfolioAnalyzer unit tests: what-if ME/TE overrides, portfolio size warning (>300 BPs) in EveMarketAnalysisClient.Tests/Unit/PortfolioAnalyzerTests.cs
- [x] T019 [P] [US1] PortfolioConfiguration validation tests: parameter bounds (slots 1-50, fees 0-100, ME 0-10, TE 0-20) in EveMarketAnalysisClient.Tests/Unit/PortfolioConfigurationTests.cs
- [x] T020 [P] [US1] PortfolioOptimizer page handler tests: OnGet, OnGetAnalysisAsync with valid/invalid parameters in EveMarketAnalysisClient.Tests/Pages/PortfolioOptimizerPageTests.cs

### Implementation for User Story 1

- [x] T021 [US1] Implement PortfolioAnalyzer.CalculateRankingsAsync: fetch blueprints, market data (parallel, SemaphoreSlim 20), fetch system cost index via ESI GET /industry/systems/ (cache 1hr, filter to configured solar system ID, extract manufacturing activity cost index), compute ISK/hr per blueprint in EveMarketAnalysisClient/Services/PortfolioAnalyzer.cs
- [x] T022 [US1] Implement ISK/hr calculation logic: material cost (highest buy at procurement station), product revenue (lowest sell at selling hub), broker fees, sales tax, system cost fee, ME/TE adjustments in EveMarketAnalysisClient/Services/PortfolioAnalyzer.cs
- [x] T023 [US1] Implement skill gating: load skill-requirements.json, cross-reference character skills (from EsiCharacterClient.GetCharacterSkillsAsync) against blueprint manufacturing prerequisites, exclude blueprints whose required skills are not met in EveMarketAnalysisClient/Services/PortfolioAnalyzer.cs
- [x] T024 [US1] Implement PortfolioOptimizer page model with OnGet and OnGetAnalysisAsync handler (accepts all config as query params) in EveMarketAnalysisClient/Pages/PortfolioOptimizer.cshtml.cs
- [x] T025 [US1] Create PortfolioOptimizer Razor page: controls panel (station selectors, ME/TE sliders, broker/tax inputs), Refresh Analysis button, skeleton loading, sortable ranking table in EveMarketAnalysisClient/Pages/PortfolioOptimizer.cshtml
- [x] T026 [US1] Implement client-side JavaScript: local storage read/write for PortfolioConfiguration, AJAX fetch on Refresh click, table rendering with multi-column sort (ISK/hr, name, profit margin, daily volume per FR-003), loading spinner in EveMarketAnalysisClient/Pages/PortfolioOptimizer.cshtml
- [x] T027 [US1] Add navigation link to PortfolioOptimizer in shared layout in EveMarketAnalysisClient/Pages/Shared/_Layout.cshtml

**Checkpoint**: User Story 1 fully functional — blueprints ranked by ISK/hr with all cost factors, sortable table, Refresh Analysis workflow

---

## Phase 4: User Story 2 — Phased Production Roadmap (Priority: P1)

**Goal**: Five phase cards with progress indicators showing owned profitable blueprint count vs required threshold, phase completion logic, and manual "Advance Phase" override.

**Independent Test**: Verify phase cards render with correct counts and phase completion triggers correctly based on slot utilization and income fallback.

### Tests for User Story 2

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [x] T028 [P] [US2] Phase completion unit tests: slot-based trigger (ceil(N×9/11)), income fallback trigger, manual override, Phase 5 terminal state in EveMarketAnalysisClient.Tests/Unit/PhaseCompletionTests.cs
- [x] T029 [P] [US2] Phase status evaluation tests: correct counting of owned profitable BPs per phase, configurable thresholds in EveMarketAnalysisClient.Tests/Unit/PhaseCompletionTests.cs

### Implementation for User Story 2

- [x] T030 [US2] Implement PortfolioAnalyzer.EvaluatePhaseStatusesAsync: compute PhaseStatus for each phase using rankings, configuration thresholds in EveMarketAnalysisClient/Services/PortfolioAnalyzer.cs
- [x] T031 [US2] Implement phase completion logic: primary trigger (slot count), secondary trigger (daily income), manual override handling, current phase determination in EveMarketAnalysisClient/Services/PortfolioAnalyzer.cs
- [x] T032 [US2] Add phase roadmap UI section: five grouped cards with progress bars, current-phase indicator, completed-phase styling, "Advance Phase" button in EveMarketAnalysisClient/Pages/PortfolioOptimizer.cshtml
- [x] T033 [US2] Add JS for phase roadmap: render phase cards from analysis response, Advance Phase button with confirmation, persist override in local storage in EveMarketAnalysisClient/Pages/PortfolioOptimizer.cshtml
- [x] T034 [US2] Wire phaseOverride query parameter from local storage through OnGetAnalysisAsync handler in EveMarketAnalysisClient/Pages/PortfolioOptimizer.cshtml.cs

**Checkpoint**: User Stories 1 AND 2 both work — ranking table plus phase roadmap with completion logic

---

## Phase 5: User Story 3 — Recommended BPO Purchases (Priority: P2)

**Goal**: List of unowned BPOs from the current phase (which shifts upon advancement) with NPC seeded price, player market price (region-wide), projected ISK/hr, ROI, and payback period.

**Independent Test**: Verify a user in Phase 1 sees Phase 1 BPO recommendations; after advancing, sees Phase 2 recommendations.

### Tests for User Story 3

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [x] T035 [P] [US3] BPO recommendation unit tests: phase scoping (current vs next post-advancement), exclusion of owned BPOs, sorting by projected ISK/hr in EveMarketAnalysisClient.Tests/Unit/BpoRecommendationTests.cs
- [x] T036 [P] [US3] BPO recommendation unit tests: NPC price lookup via ESI adjusted_price, region-wide player market price, ROI and payback calculations in EveMarketAnalysisClient.Tests/Unit/BpoRecommendationTests.cs

### Implementation for User Story 3

- [x] T037 [US3] Implement PortfolioAnalyzer.GenerateBpoRecommendationsAsync: determine recommendation phase, fetch NPC prices (GET /markets/prices), region-wide market snapshots, compute projected ISK/hr at ME10/TE20, ROI, payback in EveMarketAnalysisClient/Services/PortfolioAnalyzer.cs
- [x] T038 [US3] Add BPO recommendations UI section: table with NPC price, player price, projected ISK/hr, ROI, payback, phase label, Phase 5 empty state in EveMarketAnalysisClient/Pages/PortfolioOptimizer.cshtml
- [x] T039 [US3] Add JS for BPO recommendations: render recommendation table from analysis response in EveMarketAnalysisClient/Pages/PortfolioOptimizer.cshtml

**Checkpoint**: User Stories 1, 2, AND 3 all work — ranking, roadmap, and BPO purchase recommendations

---

## Phase 6: User Story 4 — Research Queue Recommendations (Priority: P2)

**Goal**: Prioritized list of 5–10 under-researched blueprints ordered by projected ISK/hr gain from additional ME/TE research.

**Independent Test**: Verify a user with partially-researched blueprints sees correct recommendations ordered by ISK/hr improvement.

### Tests for User Story 4

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [x] T040 [P] [US4] Research recommendation unit tests: ISK/hr gain calculation at current vs max ME/TE, sorting by gain, cap at 10 results, empty state when fully researched in EveMarketAnalysisClient.Tests/Unit/ResearchRecommendationTests.cs

### Implementation for User Story 4

- [x] T041 [US4] Implement PortfolioAnalyzer.GenerateResearchRecommendationsAsync: compare ISK/hr at current ME/TE vs ME10/TE20, sort by gain descending, cap at 10 in EveMarketAnalysisClient/Services/PortfolioAnalyzer.cs
- [x] T042 [US4] Add research recommendations UI section: table with current/projected ISK/hr, gain, current ME/TE, "all researched" empty state in EveMarketAnalysisClient/Pages/PortfolioOptimizer.cshtml
- [x] T043 [US4] Add JS for research recommendations: render table from analysis response in EveMarketAnalysisClient/Pages/PortfolioOptimizer.cshtml

**Checkpoint**: User Stories 1–4 all work — full analysis with ranking, roadmap, BPO recommendations, and research queue

---

## Phase 7: User Story 5 — Configurable Thresholds and Simulation (Priority: P3)

**Goal**: Configurable min ISK/hr, daily income goal, slot count with "Simulate Next Phase" projection previewing all sections as if in the next phase.

**Independent Test**: Adjust thresholds, click Refresh, verify phase completion and highlighting update. Click Simulate Next Phase, verify all sections shift.

### Tests for User Story 5

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [x] T044 [P] [US5] Simulation unit tests: "Simulate Next Phase" produces PortfolioAnalysis with incremented current phase, adjusted BPO recommendations, without persisting the phase change in EveMarketAnalysisClient.Tests/Unit/SimulationTests.cs
- [x] T045 [P] [US5] Threshold configuration tests: changing min ISK/hr affects profitable count, changing slot count affects required count formula in EveMarketAnalysisClient.Tests/Unit/SimulationTests.cs

### Implementation for User Story 5

- [x] T046 [US5] Implement simulation mode in PortfolioAnalyzer: accept simulateNextPhase flag, produce analysis with phase+1 without persisting in EveMarketAnalysisClient/Services/PortfolioAnalyzer.cs
- [x] T047 [US5] Add "Simulate Next Phase" button and threshold configuration controls to UI in EveMarketAnalysisClient/Pages/PortfolioOptimizer.cshtml
- [x] T048 [US5] Add JS for simulation: "Simulate Next Phase" button sends simulateNextPhase=true to handler, renders results with visual indicator that simulation is active in EveMarketAnalysisClient/Pages/PortfolioOptimizer.cshtml

**Checkpoint**: All 5 user stories functional — complete Portfolio Optimizer feature

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Integration testing, edge cases, and final validation

- [x] T049 [P] Integration tests: end-to-end PortfolioAnalyzer with mocked ESI calls covering full analysis pipeline in EveMarketAnalysisClient.Tests/Services/PortfolioAnalyzerIntegrationTests.cs
- [x] T050 [P] Edge case tests: zero blueprints empty state, missing market data, rate limit handling, portfolio size warning, local storage config round-trip serialization in EveMarketAnalysisClient.Tests/Unit/PortfolioEdgeCaseTests.cs
- [x] T051 Add edge case handling in UI: zero-blueprint empty state, "Price unavailable" display, "Low liquidity" flag, rate limit retry message, portfolio size warning in EveMarketAnalysisClient/Pages/PortfolioOptimizer.cshtml
- [x] T052 Run full test suite and validate all tests pass via dotnet test EveMarketAnalysisClient.Tests
- [x] T053 Run quickstart.md validation: build, launch, navigate to /portfoliooptimizer, verify end-to-end flow

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion — BLOCKS all user stories
- **User Story 1 (Phase 3)**: Depends on Foundational — MVP, must complete first
- **User Story 2 (Phase 4)**: Depends on Foundational — can parallel with US1 but shares PortfolioAnalyzer
- **User Story 3 (Phase 5)**: Depends on Foundational + Phase evaluation logic from US2
- **User Story 4 (Phase 6)**: Depends on Foundational + ISK/hr calculation from US1
- **User Story 5 (Phase 7)**: Depends on US1 + US2 (needs ranking + phase logic to simulate)
- **Polish (Phase 8)**: Depends on all desired user stories being complete

### User Story Dependencies

- **US1 (P1)**: Foundational only — independent core
- **US2 (P1)**: Foundational only — can develop in parallel with US1 (different methods on PortfolioAnalyzer)
- **US3 (P2)**: Requires US2's phase completion logic to determine recommendation phase
- **US4 (P2)**: Requires US1's ISK/hr calculation to compute research gains
- **US5 (P3)**: Requires US1 + US2 for simulation to have meaning

### Within Each User Story

- Tests MUST be written and FAIL before implementation (Constitution Principle III)
- Models before services
- Services before page handlers
- Page handlers before Razor views
- Core implementation before integration

### Parallel Opportunities

- T002–T010 (all model records and interfaces) can run in parallel
- T011–T012 (foundational tests) can run in parallel
- T016–T020 (US1 tests) can run in parallel
- T028–T029 (US2 tests) can run in parallel
- T035–T036 (US3 tests) can run in parallel
- US1 and US2 can be developed in parallel (different methods, different UI sections)
- US3 and US4 can be developed in parallel after their prerequisites

---

## Parallel Example: User Story 1

```bash
# Launch all US1 tests together (Red phase):
Task: "T016 PortfolioAnalyzer ISK/hr calculation tests"
Task: "T017 PortfolioAnalyzer ranking and skill gating tests"
Task: "T018 PortfolioAnalyzer what-if and portfolio size tests"
Task: "T019 PortfolioConfiguration validation tests"
Task: "T020 PortfolioOptimizer page handler tests"

# Launch model records together (already done in Phase 1):
Task: "T005 BlueprintRankingEntry record"
Task: "T008 PortfolioAnalysis record"
Task: "T004 PortfolioConfiguration record"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (models, interfaces, phases.json)
2. Complete Phase 2: Foundational (PhaseService, region-wide market client)
3. Complete Phase 3: User Story 1 (ISK/hr ranking with Refresh Analysis)
4. **STOP and VALIDATE**: Test ranking table with real character data
5. Deploy/demo if ready

### Incremental Delivery

1. Setup + Foundational → Foundation ready
2. Add US1 → ISK/hr ranking table → Deploy/Demo (MVP!)
3. Add US2 → Phase roadmap with completion logic → Deploy/Demo
4. Add US3 → BPO purchase recommendations → Deploy/Demo
5. Add US4 → Research queue → Deploy/Demo
6. Add US5 → Thresholds and simulation → Deploy/Demo
7. Polish → Edge cases, integration tests → Final release

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Constitution requires TDD: all test tasks MUST precede their implementation tasks
- All new models MUST be immutable records with ImmutableArray collections
- All service methods MUST be pure functions returning immutable results
- Market data MUST use IMemoryCache with 5-min sliding expiration
- Concurrent ESI calls MUST be limited to 20 via SemaphoreSlim
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
