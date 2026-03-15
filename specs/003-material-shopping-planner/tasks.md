# Tasks: Material Shopping List Planner

**Input**: Design documents from `/specs/003-material-shopping-planner/`
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, contracts/

**Tests**: TDD is mandatory per constitution Principle III. Tests are written FIRST and must FAIL before implementation.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Phase 1: Foundational (Shared Models & Interfaces)

**Purpose**: Immutable record types, service interface, and DI registration that ALL user stories depend on

**CRITICAL**: No user story work can begin until this phase is complete

- [ ] T001 [P] Create `BlueprintSelection` record in `EveMarketAnalysisClient/Models/BlueprintSelection.cs` per data-model.md (blueprint identity, runs, ME/TE, IsCopy, MaxRuns, ProduceComponents, ProducedTypeId)
- [ ] T002 [P] Create `MaterialTreeNode` record in `EveMarketAnalysisClient/Models/MaterialTreeNode.cs` per data-model.md (TypeId, TypeName, BaseQuantity, AdjustedQuantity, Runs, TotalQuantity, IsExpanded, SourceBlueprintTypeId, Children as `ImmutableArray<MaterialTreeNode>`)
- [ ] T003 [P] Create `MaterialSource` record in `EveMarketAnalysisClient/Models/MaterialSource.cs` per data-model.md (BlueprintName, BlueprintTypeId, Quantity)
- [ ] T004 [P] Create `ShoppingListItem` record in `EveMarketAnalysisClient/Models/ShoppingListItem.cs` per data-model.md (TypeId, TypeName, Category, TotalQuantity, Volume, TotalVolume, EstimatedUnitCost, EstimatedTotalCost, Sources as `ImmutableArray<MaterialSource>`)
- [ ] T005 [P] Create `ShoppingListResponse` record in `EveMarketAnalysisClient/Models/ShoppingListResponse.cs` per data-model.md (Items, TotalEstimatedCost, TotalVolume, BlueprintCount, GeneratedAt, Errors)
- [ ] T006 Create `IShoppingListService` interface in `EveMarketAnalysisClient/Services/Interfaces/IShoppingListService.cs` with methods: `BuildOwnedBlueprintMap`, `ExpandBlueprintToMaterials`, `AggregateMaterials`, `GenerateShoppingListAsync`, `FetchCostsAsync`, `FetchVolumesAsync`, `GenerateCsv`, `GenerateClipboardText`
- [ ] T007 Register `IShoppingListService` / `ShoppingListService` as scoped service in `EveMarketAnalysisClient/Program.cs` (depends on T006 — interface must exist before registration compiles)

**Checkpoint**: All shared types defined and DI registered. User story implementation can now begin.

---

## Phase 2: User Story 1 - Select Blueprints and Generate Material List (Priority: P1) MVP

**Goal**: Authenticated user can view owned blueprints, select multiple with run counts, and generate an aggregated shopping list showing material names and quantities (no recursive expansion yet, no costs).

**Independent Test**: Log in, navigate to `/productionplanner`, see blueprint list, select blueprints, set runs, click "Generate List", verify aggregated material quantities with ME applied. Duplicate materials across blueprints are merged and summed.

### Tests for User Story 1

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T008 [P] [US1] Unit tests for ME calculation: `max(1, ceil(base * (1 - ME/100)))` for various ME levels (0, 5, 10) and base quantities in `EveMarketAnalysisClient.Tests/Services/ShoppingListServiceTests.cs`
- [ ] T009 [P] [US1] Unit tests for flat material expansion (single blueprint, no recursion): verify `ExpandBlueprintToMaterials` returns correct `MaterialTreeNode` with adjusted quantities for a known blueprint in `EveMarketAnalysisClient.Tests/Models/MaterialTreeNodeTests.cs`
- [ ] T010 [P] [US1] Unit tests for `AggregateMaterials`: verify duplicate TypeIds across multiple trees are merged, quantities summed, and `MaterialSource` entries tracked in `EveMarketAnalysisClient.Tests/Services/ShoppingListServiceTests.cs`
- [ ] T011 [P] [US1] Unit tests for `BuildOwnedBlueprintMap`: verify `FrozenDictionary<int, CharacterBlueprint>` keyed by ProducedTypeId, highest-ME blueprint wins when duplicates exist in `EveMarketAnalysisClient.Tests/Services/ShoppingListServiceTests.cs`
- [ ] T012 [P] [US1] Page handler tests for `OnGetBlueprintsAsync`: verify returns enriched blueprint JSON with producedTypeId/producedTypeName, and returns error when unauthenticated in `EveMarketAnalysisClient.Tests/Pages/ProductionPlannerTests.cs`
- [ ] T013 [P] [US1] Page handler tests for `OnGetShoppingListAsync`: verify returns `ShoppingListResponse` JSON matching contract, and returns error for empty selections in `EveMarketAnalysisClient.Tests/Pages/ProductionPlannerTests.cs`
- [ ] T014 [P] [US1] Integration test for `GenerateShoppingListAsync` end-to-end: mock `IEsiCharacterClient` and `IBlueprintDataService`, verify full pipeline (fetch blueprints → build map → expand → aggregate → resolve names) returns correct `ShoppingListResponse` in `EveMarketAnalysisClient.Tests/Services/ShoppingListServiceTests.cs`

### Implementation for User Story 1

- [ ] T015 [US1] Implement `BuildOwnedBlueprintMap` in `EveMarketAnalysisClient/Services/ShoppingListService.cs`: takes `ImmutableArray<CharacterBlueprint>`, uses `IBlueprintDataService` to look up `ProducedTypeId` for each, builds `FrozenDictionary<int, CharacterBlueprint>` keyed by producedTypeId (highest ME wins on duplicates)
- [ ] T016 [US1] Implement `ExpandBlueprintToMaterials` (flat, non-recursive) in `EveMarketAnalysisClient/Services/ShoppingListService.cs`: takes `BlueprintSelection` and owned map, looks up `BlueprintActivity` from `IBlueprintDataService`, applies ME formula to each material, returns `MaterialTreeNode` with `IsExpanded = false` and empty Children
- [ ] T017 [US1] Implement `AggregateMaterials` in `EveMarketAnalysisClient/Services/ShoppingListService.cs`: takes `ImmutableArray<MaterialTreeNode>` trees, flattens leaf nodes, groups by TypeId, sums TotalQuantity, collects `MaterialSource` entries per item, returns `ImmutableArray<ShoppingListItem>` (costs null, volume 0 initially)
- [ ] T018 [US1] Implement `GenerateShoppingListAsync` in `EveMarketAnalysisClient/Services/ShoppingListService.cs`: orchestrates BuildOwnedBlueprintMap → expand each selection → aggregate → resolve type names via bulk `POST /universe/names` → return `ShoppingListResponse`
- [ ] T019 [US1] Create `ProductionPlanner.cshtml.cs` page model in `EveMarketAnalysisClient/Pages/ProductionPlanner.cshtml.cs`: `[Authorize]` attribute, `OnGet` extracts character from claims, `OnGetBlueprintsAsync` handler fetches and enriches blueprints, `OnGetShoppingListAsync` handler parses selections JSON and delegates to `IShoppingListService`
- [ ] T020 [US1] Create `ProductionPlanner.cshtml` Razor view in `EveMarketAnalysisClient/Pages/ProductionPlanner.cshtml`: left panel with blueprint list (checkboxes, name, ME/TE, type, runs input), central panel with shopping list table (Material Name, Quantity columns), skeleton loading placeholders, "Generate List" and "Clear Selection" buttons, empty state for no blueprints, JS to call handlers and render results

**Checkpoint**: User Story 1 fully functional — users can select blueprints, set runs, generate flat material list with ME applied and duplicates aggregated.

---

## Phase 3: User Story 2 - Recursive Component Production (Priority: P1)

**Goal**: When "Produce Components" is toggled on for a blueprint, the system recursively substitutes intermediate materials with sub-materials from owned blueprints, applying each sub-blueprint's own ME. Cycle detection prevents infinite recursion.

**Independent Test**: Select a blueprint requiring intermediate components, toggle "Produce Components" on, verify intermediates are replaced by raw materials (only when owned). Toggle off and verify only top-level materials shown. Test multi-level chains (3+ deep).

### Tests for User Story 2

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T021 [P] [US2] Unit tests for recursive expansion WITH owned component blueprints: verify intermediate is replaced by sub-materials with sub-blueprint's ME applied in `EveMarketAnalysisClient.Tests/Models/MaterialTreeNodeTests.cs`
- [ ] T022 [P] [US2] Unit tests for recursive expansion WITHOUT owned component: verify intermediate remains as leaf node (not expanded) in `EveMarketAnalysisClient.Tests/Models/MaterialTreeNodeTests.cs`
- [ ] T023 [P] [US2] Unit tests for multi-level recursion (3+ levels deep): component A → component B → raw materials, verify full chain resolves correctly with per-level ME in `EveMarketAnalysisClient.Tests/Models/MaterialTreeNodeTests.cs`
- [ ] T024 [P] [US2] Unit tests for cycle detection: verify circular dependency (A requires B, B requires A) is broken and does not cause infinite recursion in `EveMarketAnalysisClient.Tests/Models/MaterialTreeNodeTests.cs`
- [ ] T025 [P] [US2] Unit tests for ProduceComponents=false: verify no recursive expansion occurs even when owned blueprints exist for intermediates in `EveMarketAnalysisClient.Tests/Models/MaterialTreeNodeTests.cs`
- [ ] T026 [P] [US2] Unit tests for aggregation after recursive expansion: verify same raw material from different branches is correctly merged in `EveMarketAnalysisClient.Tests/Services/ShoppingListServiceTests.cs`

### Implementation for User Story 2

- [ ] T027 [US2] Extend `ExpandBlueprintToMaterials` in `EveMarketAnalysisClient/Services/ShoppingListService.cs` with recursive logic: when `ProduceComponents=true` and owned map contains a blueprint for the material's TypeId, recursively expand using the sub-blueprint's activity and ME. Pass `ImmutableHashSet<int>` of visited producedTypeIds for cycle detection. Set `IsExpanded=true` and populate `Children` on expanded nodes.
- [ ] T028 [US2] Update `AggregateMaterials` in `EveMarketAnalysisClient/Services/ShoppingListService.cs` to recursively collect only leaf nodes (where `IsExpanded=false`) from the material tree before grouping

**Checkpoint**: User Stories 1 AND 2 fully functional — recursive expansion works with per-blueprint ME and cycle detection.

---

## Phase 4: User Story 3 - Procurement Cost Estimation (Priority: P2)

**Goal**: Users see estimated market costs per material and total ISK based on a selected trade hub region. Changing region refreshes only costs, not the material list.

**Independent Test**: Generate a material list, select a region, verify cost and volume columns populate. Change region, verify only costs refresh (quantities unchanged). Verify "N/A" for unavailable market data.

### Tests for User Story 3

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T029 [P] [US3] Unit tests for `FetchCostsAsync`: verify parallel market calls via mocked `IEsiMarketClient`, verify `LowestSellPrice` mapped to items, verify null handling for unavailable prices in `EveMarketAnalysisClient.Tests/Services/ShoppingListServiceTests.cs`
- [ ] T030 [P] [US3] Unit tests for `FetchVolumesAsync`: verify parallel volume lookups via mocked ESI `/universe/types/{typeId}`, verify volume mapped to items, verify fallback to 0.0 when unavailable in `EveMarketAnalysisClient.Tests/Services/ShoppingListServiceTests.cs`
- [ ] T031 [P] [US3] Page handler tests for `OnGetCostsAsync`: verify returns cost JSON per contract (including volume data), verify region validation against `TradeHubRegion.All`, verify error for invalid regionId in `EveMarketAnalysisClient.Tests/Pages/ProductionPlannerTests.cs`
- [ ] T032 [P] [US3] Integration test for cost fetch pipeline: mock `IEsiMarketClient`, verify `FetchCostsAsync` dispatches parallel calls with `SemaphoreSlim(20)` throttle, verify caching behavior (second call for same region/typeId returns cached data) in `EveMarketAnalysisClient.Tests/Services/ShoppingListServiceTests.cs`

### Implementation for User Story 3

- [ ] T033 [US3] Implement `FetchCostsAsync` in `EveMarketAnalysisClient/Services/ShoppingListService.cs`: takes `ImmutableArray<int>` typeIds and regionId, dispatches `EsiMarketClient.GetMarketSnapshotAsync` in parallel with `SemaphoreSlim(20)`, returns `FrozenDictionary<int, decimal?>` mapping typeId → lowestSellPrice
- [ ] T034 [US3] Implement `FetchVolumesAsync` in `EveMarketAnalysisClient/Services/ShoppingListService.cs`: takes `ImmutableArray<int>` typeIds, fetches volume from `GET /universe/types/{typeId}` in parallel with `SemaphoreSlim(20)`, returns `FrozenDictionary<int, double>` mapping typeId → volume in m³ (cached 24h via `IMemoryCache`)
- [ ] T035 [US3] Add `OnGetCostsAsync` handler to `EveMarketAnalysisClient/Pages/ProductionPlanner.cshtml.cs`: parses regionId (default 10000002) and comma-separated typeIds, validates regionId against `TradeHubRegion.All`, delegates to `IShoppingListService.FetchCostsAsync` and `FetchVolumesAsync`, returns JSON per costs contract (including per-item volume)
- [ ] T036 [US3] Update `ProductionPlanner.cshtml` in `EveMarketAnalysisClient/Pages/ProductionPlanner.cshtml`: add region dropdown (5 trade hubs from `TradeHubRegion.All`), Estimated Cost and Volume columns to shopping list table, total ISK row at bottom, JS to call `OnGetCostsAsync` on region change and update only cost/volume cells without re-rendering the material list

**Checkpoint**: Cost estimation works independently of list generation. Region changes refresh costs only.

---

## Phase 5: User Story 4 - Export and Clipboard Actions (Priority: P2)

**Goal**: Users can export the shopping list as CSV or copy to clipboard for use in spreadsheets and external tools.

**Independent Test**: Generate a list (with or without costs), click "Export CSV" and verify valid CSV downloads. Click "Copy to Clipboard" and verify tab-separated text is copied.

### Tests for User Story 4

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T037 [P] [US4] Unit tests for `GenerateCsv`: verify CSV output with headers, correct columns, proper escaping of commas/quotes, costs included when available and omitted when null in `EveMarketAnalysisClient.Tests/Services/ShoppingListServiceTests.cs`
- [ ] T038 [P] [US4] Unit tests for `GenerateClipboardText`: verify tab-separated format, verify costs included/omitted based on availability in `EveMarketAnalysisClient.Tests/Services/ShoppingListServiceTests.cs`

### Implementation for User Story 4

- [ ] T039 [US4] Implement `GenerateCsv` and `GenerateClipboardText` as pure functions in `EveMarketAnalysisClient/Services/ShoppingListService.cs`: take `ImmutableArray<ShoppingListItem>`, return string. CSV: comma-delimited with header row. Clipboard: tab-separated. Both include Material Name, Quantity, Category; include Cost and Volume columns only if cost data loaded.
- [ ] T040 [US4] Add "Export CSV" and "Copy to Clipboard" buttons to `EveMarketAnalysisClient/Pages/ProductionPlanner.cshtml`: JS generates CSV blob and triggers download, JS copies tab-separated text to clipboard via `navigator.clipboard.writeText`. Buttons disabled when no shopping list generated.

**Checkpoint**: Export and clipboard fully functional. Buttons disabled when no list present.

---

## Phase 6: User Story 5 - Material Source Details (Priority: P3)

**Goal**: Each material row in the shopping list is expandable to show which blueprint(s) require it and their individual quantity contributions.

**Independent Test**: Generate a list from multiple blueprints sharing a common material, expand that row, verify per-blueprint breakdown with correct quantities.

### Tests for User Story 5

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T041 [P] [US5] Unit tests for expandable source details: verify `ShoppingListItem.Sources` contains correct per-blueprint breakdown when material is required by multiple blueprints, and verify single-source materials have exactly one `MaterialSource` entry in `EveMarketAnalysisClient.Tests/Services/ShoppingListServiceTests.cs`
- [ ] T042 [P] [US5] Page handler tests for source detail rendering: verify JSON response includes `sources` array for each item with correct blueprintName, blueprintTypeId, and quantity values in `EveMarketAnalysisClient.Tests/Pages/ProductionPlannerTests.cs`

### Implementation for User Story 5

- [ ] T043 [US5] Add expandable detail rows to the shopping list table in `EveMarketAnalysisClient/Pages/ProductionPlanner.cshtml`: each material row has a toggle/chevron, clicking it reveals a sub-table showing `MaterialSource` entries (blueprint name and quantity contribution). Use CSS collapse/expand with JS toggle. Sources data already available from `ShoppingListItem.Sources` populated in US1 aggregation.

**Checkpoint**: All 5 user stories independently functional.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: UX polish, search filter, and validation

- [ ] T044 [P] Add text search filter for blueprint list in `EveMarketAnalysisClient/Pages/ProductionPlanner.cshtml`: text input above blueprint list that filters by name client-side (JS `input` event, case-insensitive match on blueprint name). Implements FR-003a.
- [ ] T045 [P] Add loading states and error handling to `EveMarketAnalysisClient/Pages/ProductionPlanner.cshtml`: skeleton placeholders during blueprint fetch, spinner during list generation, loading indicators on cost column during cost fetch, error alerts for API failures, "No blueprints found" empty state
- [ ] T046 [P] Add BPC run validation to `EveMarketAnalysisClient/Pages/ProductionPlanner.cshtml`: for BPC blueprints, cap numeric input max to available runs, show warning if user attempts to exceed
- [ ] T047 [P] Add material category grouping to shopping list table in `EveMarketAnalysisClient/Pages/ProductionPlanner.cshtml`: group `ShoppingListItem` rows by `Category` field (from SDE market group data) with category headers, "Uncategorized" fallback, totals row at bottom showing total quantity, volume, and cost
- [ ] T048 [P] Add graceful session expiry handling to `EveMarketAnalysisClient/Pages/ProductionPlanner.cshtml`: JS fetch layer detects 401/redirect responses, shows re-login prompt to user, optionally preserves blueprint selection state in `sessionStorage` so selections survive re-authentication
- [ ] T049 Run quickstart.md validation: build solution, run all tests, start app, navigate to `/productionplanner`, verify full workflow end-to-end

---

## Dependencies & Execution Order

### Phase Dependencies

- **Foundational (Phase 1)**: No dependencies — can start immediately. BLOCKS all user stories.
- **US1 (Phase 2)**: Depends on Foundational — core MVP
- **US2 (Phase 3)**: Depends on US1 (extends `ExpandBlueprintToMaterials`)
- **US3 (Phase 4)**: Depends on US1 (needs shopping list items to price)
- **US4 (Phase 5)**: Depends on US1 (needs shopping list items to export); can run in parallel with US3
- **US5 (Phase 6)**: Depends on US1 (needs `MaterialSource` data from aggregation); can run in parallel with US3/US4
- **Polish (Phase 7)**: Depends on US1 at minimum; ideally after all stories

### User Story Dependencies

- **US1 (P1)**: Can start after Foundational (Phase 1) — No dependencies on other stories
- **US2 (P1)**: Depends on US1 — extends the same expansion function
- **US3 (P2)**: Depends on US1 — needs generated list to price. Can run in parallel with US4/US5
- **US4 (P2)**: Depends on US1 — needs generated list to export. Can run in parallel with US3/US5
- **US5 (P3)**: Depends on US1 — needs Sources data from aggregation. Can run in parallel with US3/US4

### Within Each User Story

- Tests MUST be written and FAIL before implementation
- Models before services
- Services before page handlers
- Page handlers before Razor view
- Core logic before UI integration

### Parallel Opportunities

- Phase 1: All model records (T001-T005) can be created in parallel
- Phase 2 tests: All test tasks (T008-T014) can be written in parallel
- Phase 3 tests: All test tasks (T021-T026) can be written in parallel
- Phase 4 tests: T029-T032 can be written in parallel
- Phase 5 tests: T037 and T038 can be written in parallel
- Phase 6 tests: T041 and T042 can be written in parallel
- After US1 complete: US3, US4, and US5 can proceed in parallel
- Phase 7: All polish tasks (T044-T048) can proceed in parallel

---

## Parallel Example: User Story 1

```bash
# Launch all US1 tests in parallel (write first, must fail):
Task: "T008 Unit tests for ME calculation in ShoppingListServiceTests.cs"
Task: "T009 Unit tests for flat material expansion in MaterialTreeNodeTests.cs"
Task: "T010 Unit tests for AggregateMaterials in ShoppingListServiceTests.cs"
Task: "T011 Unit tests for BuildOwnedBlueprintMap in ShoppingListServiceTests.cs"
Task: "T012 Page handler tests for OnGetBlueprintsAsync in ProductionPlannerTests.cs"
Task: "T013 Page handler tests for OnGetShoppingListAsync in ProductionPlannerTests.cs"
Task: "T014 Integration test for GenerateShoppingListAsync in ShoppingListServiceTests.cs"

# Then sequential service implementation:
Task: "T015 BuildOwnedBlueprintMap"
Task: "T016 ExpandBlueprintToMaterials (flat)"
Task: "T017 AggregateMaterials"
Task: "T018 GenerateShoppingListAsync"
Task: "T019 ProductionPlanner.cshtml.cs"
Task: "T020 ProductionPlanner.cshtml"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Foundational models and DI (T001-T007)
2. Complete Phase 2: User Story 1 tests then implementation (T008-T020)
3. **STOP and VALIDATE**: Flat material list with ME works end-to-end
4. Deploy/demo if ready — users can already plan simple batches

### Incremental Delivery

1. Foundational → Foundation ready
2. User Story 1 → Flat material list MVP → Deploy/Demo
3. User Story 2 → Recursive expansion → Deploy/Demo (major value add)
4. User Story 3 → Cost estimation with volume → Deploy/Demo
5. User Story 4 → Export/clipboard → Deploy/Demo
6. User Story 5 → Source details → Deploy/Demo
7. Polish → Search filter, loading states, session handling, validation → Final release

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Constitution Principle III enforced: every task phase lists tests before implementation
- All new models are immutable records per constitution Principle I
- Reuse existing services: `BlueprintDataService`, `EsiMarketClient`, `EsiCharacterClient`, `TradeHubRegion`
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
