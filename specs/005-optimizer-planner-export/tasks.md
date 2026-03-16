# Tasks: Optimizer-to-Planner Export Integration (One-Click Batch Production)

**Input**: Design documents from `/specs/005-optimizer-planner-export/`
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, contracts/

**Tests**: Included per constitution requirement (TDD is NON-NEGOTIABLE). Tests MUST be written and FAIL before implementation.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Create the test directory structure needed for new test files

- [ ] T001 Create `EveMarketAnalysisClient.Tests/Models/` directory if it does not exist

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: C# model defining the JSON export contract — shared by all user stories and required for TDD

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

### Tests for Foundational Phase

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T002 Write serialization round-trip tests for `ProductionBatchExport` and `BlueprintRunExport` records in `EveMarketAnalysisClient.Tests/Models/ProductionBatchExportTests.cs`. Tests MUST verify: (a) a `ProductionBatchExport` with multiple `BlueprintRunExport` items round-trips through `System.Text.Json.JsonSerializer` with camelCase property naming, (b) deserialized `TypeId`, `Name`, and `Runs` values match originals, (c) `ImmutableArray<BlueprintRunExport>` serializes as a JSON array.
- [ ] T003 [P] Write runs-clamping tests in `EveMarketAnalysisClient.Tests/Models/ProductionBatchExportTests.cs`. Tests MUST verify: (a) `BlueprintRunExport.ClampRuns(0)` returns 1, (b) `ClampRuns(-5)` returns 1, (c) `ClampRuns(10)` returns 10, (d) `ClampRuns(1)` returns 1. This tests a static pure function `BlueprintRunExport.ClampRuns(int runs)`.

### Implementation for Foundational Phase

- [ ] T004 Create `ProductionBatchExport` and `BlueprintRunExport` immutable records in `EveMarketAnalysisClient/Models/ProductionBatchExport.cs`. `ProductionBatchExport` is `record(ImmutableArray<BlueprintRunExport> Items)`. `BlueprintRunExport` is `record(int TypeId, string Name, int Runs)` with a static pure method `ClampRuns(int runs) => Math.Max(1, runs)`. Add `[JsonPropertyName]` attributes for camelCase JSON serialization matching the localStorage contract: `{"items":[{"typeId":691,"name":"Rifter","runs":5}]}`. Include `using System.Collections.Immutable` and `using System.Text.Json.Serialization`.
- [ ] T005 Verify all tests pass: run `dotnet test EveMarketAnalysisClient.Tests --filter FullyQualifiedName~ProductionBatchExport`

**Checkpoint**: Foundation ready — C# contract model tested and passing. User story implementation can now begin.

---

## Phase 3: User Story 1 - Select and Export Blueprints from Optimizer (Priority: P1) 🎯 MVP

**Goal**: Add a checkbox column to the optimizer rankings table and an "Export to Planner" button that serializes selected blueprints to localStorage and opens the planner in a new tab.

**Independent Test**: Select blueprints in optimizer, click Export, verify `pendingProductionBatch` appears in browser localStorage with correct JSON structure. Planner import not required to validate this story.

### Tests for User Story 1

- [ ] T006 [US1] Write test in `EveMarketAnalysisClient.Tests/Models/ProductionBatchExportTests.cs` that verifies creating a `ProductionBatchExport` from a list of `(typeId, name, runs)` tuples: (a) items with runs=0 are clamped to 1, (b) items with valid runs are preserved, (c) result is an `ImmutableArray` with correct count. This tests the payload-creation logic that the JS will mirror.

### Implementation for User Story 1

- [ ] T007 [US1] Add checkbox column to the rankings table header in `EveMarketAnalysisClient/Pages/PortfolioOptimizer.cshtml`. Insert a new `<th>` as the first column with a "Select All" checkbox (`id="select-all-rankings"`) — this is a UX convenience beyond FR-001's per-row requirement. In the `renderRankings()` JS function, prepend each row with a `<td>` containing `<input type="checkbox" class="ranking-check" data-typeid="${entry.blueprint.typeId}" data-name="${entry.producedTypeName}" data-runs="${runs}">`.
- [ ] T008 [US1] Add "Export to Planner" button in `EveMarketAnalysisClient/Pages/PortfolioOptimizer.cshtml`. Place it in the rankings section header area (near the existing "Export CSV" button). Button HTML: `<button id="export-to-planner" class="btn btn-sm btn-outline-success" disabled title="Select blueprints to export">Export to Planner</button>`. Style consistently with the existing dark theme.
- [ ] T009 [US1] Implement checkbox interaction JS in `EveMarketAnalysisClient/Pages/PortfolioOptimizer.cshtml`. Add event listeners: (a) each `.ranking-check` change updates the export button label to "Export N to Planner" and enables/disables it based on checked count, (b) `#select-all-rankings` toggles all visible `.ranking-check` checkboxes, (c) update button count on each change. Use `document.querySelectorAll('.ranking-check:checked').length` to count.
- [ ] T010 [US1] Implement `exportToPlanner()` JS function in `EveMarketAnalysisClient/Pages/PortfolioOptimizer.cshtml`. On Export button click: (a) collect all checked `.ranking-check` elements, (b) for each, read `data-typeid`, `data-name`, `data-runs` attributes, (c) clamp runs to `Math.max(1, parseInt(runs) || 1)`, (d) build payload object `{items: [{typeId, name, runs}, ...]}`, (e) try `localStorage.setItem('pendingProductionBatch', JSON.stringify(payload))` — on failure show error via existing notification pattern, (f) on success call `window.open('/productionplanner', '_blank')`. Note: `setItem` is intentionally last-write-wins — multiple rapid exports overwrite the previous key (edge case per spec).

**Checkpoint**: User Story 1 complete — optimizer export works independently. Can verify by checking localStorage in browser DevTools after export.

---

## Phase 4: User Story 2 - Auto-Import Exported Blueprints in Production Planner (Priority: P1)

**Goal**: The planner detects a pending export batch on page load, auto-selects matching blueprints with imported run counts, shows a notification, and clears the localStorage key.

**Independent Test**: Manually set `localStorage.setItem('pendingProductionBatch', '{"items":[{"typeId":691,"name":"Rifter","runs":5}]}')` in browser console, then load Production Planner page. Verify blueprint is auto-checked with runs=5 and notification appears.

### Tests for User Story 2

- [ ] T011 [US2] Write test in `EveMarketAnalysisClient.Tests/Models/ProductionBatchExportTests.cs` that verifies JSON deserialization of a `ProductionBatchExport` from a raw JSON string matching the localStorage contract format. Test: (a) valid JSON with 3 items deserializes to `ProductionBatchExport` with 3 `BlueprintRunExport` items, (b) each item has correct `TypeId`, `Name`, `Runs` values, (c) empty `{"items":[]}` deserializes to empty `ImmutableArray`.

### Implementation for User Story 2

- [ ] T012 [US2] Implement `importFromOptimizer()` JS function in `EveMarketAnalysisClient/Pages/ProductionPlanner.cshtml`. This function: (a) reads `localStorage.getItem('pendingProductionBatch')`, (b) if null/empty, returns immediately (no-op), (c) parses JSON with try-catch (on error, removes key and returns), (d) iterates `payload.items`, for each item finds `.bp-check[data-typeid="${item.typeId}"]` — if not found, skips, (e) sets checkbox to `checked = true`, (f) sets `.bp-runs[data-typeid="${item.typeId}"]` value to `item.runs`, (g) counts successfully matched items, (h) calls `localStorage.removeItem('pendingProductionBatch')`, (i) if matched > 0, calls `showImportNotification(matched)`.
- [ ] T013 [US2] Call `importFromOptimizer()` at the end of the existing blueprint-rendering callback in `EveMarketAnalysisClient/Pages/ProductionPlanner.cshtml`. In the `loadBlueprints()` function's fetch success handler, after the blueprint list is rendered into the DOM, add the call to `importFromOptimizer()`. This ensures blueprint DOM elements exist before the import tries to find them by `data-typeid`.
- [ ] T014 [US2] Implement `showImportNotification(count)` JS function in `EveMarketAnalysisClient/Pages/ProductionPlanner.cshtml`. Creates an inline alert div at the top of the blueprint list panel: `<div class="alert alert-info alert-dismissible fade show" style="...eve-dark-theme-styles...">Imported ${count} blueprints from Optimizer <button type="button" class="btn-close btn-close-white" data-bs-dismiss="alert"></button></div>`. Auto-dismiss after 5 seconds via `setTimeout(() => alert.remove(), 5000)`.

**Checkpoint**: User Stories 1 AND 2 complete — full export-import loop works end-to-end. Can verify by running optimizer analysis, exporting, and seeing planner auto-populate.

---

## Phase 5: User Story 3 - Merge Imported Blueprints with Existing Planner Selections (Priority: P2)

**Goal**: When the planner already has selections, imported blueprints merge non-destructively — existing selections are preserved, conflicts are resolved by import-wins.

**Independent Test**: Pre-select blueprints A and B in planner manually. Set localStorage with batch containing B and C. Reload planner. Verify A is still selected (original runs), B has updated runs, and C is newly selected.

### Tests for User Story 3

- [ ] T015 [US3] Write merge-logic test in `EveMarketAnalysisClient.Tests/Models/ProductionBatchExportTests.cs`. Test a static pure function `ProductionBatchExport.MergeSelections(ImmutableArray<BlueprintRunExport> existing, ImmutableArray<BlueprintRunExport> incoming)` that returns merged `ImmutableArray<BlueprintRunExport>`. Verify: (a) existing items not in incoming are preserved, (b) incoming items not in existing are added, (c) items in both use incoming's runs value (import-wins), (d) result contains no duplicate TypeIds.

### Implementation for User Story 3

- [ ] T016 [US3] Implement static `MergeSelections` method on `ProductionBatchExport` in `EveMarketAnalysisClient/Models/ProductionBatchExport.cs`. Pure function: takes existing and incoming `ImmutableArray<BlueprintRunExport>`, builds a dictionary keyed by `TypeId` from existing, then overwrites/adds from incoming, returns `ImmutableArray` of merged values. No mutation of inputs. Note: this is a contract mirror for testability — the actual runtime merge happens in JS via T012's import loop, which inherently preserves existing selections.
- [ ] T017 [US3] Verify `importFromOptimizer()` in `EveMarketAnalysisClient/Pages/ProductionPlanner.cshtml` already handles merge correctly. The implementation from T012 iterates items and sets checkboxes/runs without first clearing existing selections — this is the correct merge behavior. Verify by reviewing: (a) no `uncheckAll()` or similar call before the import loop, (b) only touched blueprints are modified, (c) existing untouched selections remain checked. Add a code comment documenting the merge behavior: `// Import merges with existing selections (FR-013): only touched blueprints are modified`.
- [ ] T018 [US3] Run full test suite: `dotnet test EveMarketAnalysisClient.Tests` to verify all new and existing tests pass.

**Checkpoint**: All user stories complete — export, import, and merge all functional.

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Edge case handling, UX refinements, and final validation

- [ ] T019 Add localStorage availability check in `EveMarketAnalysisClient/Pages/PortfolioOptimizer.cshtml` export function. Before `localStorage.setItem()`, test with a try-catch write/read/remove of a sentinel key. On failure, show an inline error message: "Cannot export: browser storage is unavailable" and do not navigate.
- [ ] T020 [P] Persist checkbox state across table re-sort/re-render in `EveMarketAnalysisClient/Pages/PortfolioOptimizer.cshtml`. Maintain a `Set` of checked typeIds outside `renderRankings()`. When `renderRankings()` rebuilds the table body (destroying existing checkboxes), the new checkbox `<input>` elements created by T007 carry `data-typeid`, `data-name`, `data-runs` attributes — after render, iterate the Set and re-check matching checkboxes. Update the Export button count to reflect currently checked items. This ensures both checkbox state and data attributes survive re-sorting.
- [ ] T021 Run quickstart.md manual validation: build, test, and perform the full end-to-end verification steps listed in `specs/005-optimizer-planner-export/quickstart.md`.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Phase 1 — BLOCKS all user stories
- **User Story 1 (Phase 3)**: Depends on Phase 2 (needs C# model as contract reference)
- **User Story 2 (Phase 4)**: Depends on Phase 2 (needs C# model for deserialization test). Can run in parallel with US1 (different files).
- **User Story 3 (Phase 5)**: Depends on Phase 2 (C# model). Depends on US2 (T012 import function must exist before T017 review). Can run C# tasks (T015, T016) in parallel with US1/US2.
- **Polish (Phase 6)**: Depends on all user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Independent — modifies only `PortfolioOptimizer.cshtml`
- **User Story 2 (P1)**: Independent — modifies only `ProductionPlanner.cshtml`
- **User Story 3 (P2)**: C# merge method (T015-T016) is independent. JS review (T017) depends on US2's T012 being complete.

### Within Each User Story

- Tests MUST be written and FAIL before implementation
- Model/contract tasks before service/UI tasks
- Core implementation before edge case handling

### Parallel Opportunities

- T002 and T003 can run in parallel (different test methods, same file but independent)
- US1 (Phase 3) and US2 (Phase 4) can run in parallel (different Razor files)
- T015-T016 (US3 C# tasks) can run in parallel with US1/US2 implementation
- T019 and T020 (Polish) can run in parallel (independent concerns)

---

## Parallel Example: User Stories 1 and 2

```bash
# After Phase 2 foundational is complete, launch in parallel:

# Developer A (or Agent Worker A): User Story 1
Task: "T007 [US1] Add checkbox column to rankings table in PortfolioOptimizer.cshtml"
Task: "T008 [US1] Add Export to Planner button in PortfolioOptimizer.cshtml"
Task: "T009 [US1] Implement checkbox interaction JS in PortfolioOptimizer.cshtml"
Task: "T010 [US1] Implement exportToPlanner() JS in PortfolioOptimizer.cshtml"

# Developer B (or Agent Worker B): User Story 2
Task: "T012 [US2] Implement importFromOptimizer() JS in ProductionPlanner.cshtml"
Task: "T013 [US2] Call importFromOptimizer() after blueprint render in ProductionPlanner.cshtml"
Task: "T014 [US2] Implement showImportNotification() JS in ProductionPlanner.cshtml"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (directory)
2. Complete Phase 2: Foundational (C# model + tests)
3. Complete Phase 3: User Story 1 (optimizer export)
4. **STOP and VALIDATE**: Verify export creates correct localStorage payload
5. Export side is independently useful as a data staging mechanism

### Incremental Delivery

1. Complete Setup + Foundational → Contract model ready
2. Add User Story 1 → Test export independently → Validate localStorage payload
3. Add User Story 2 → Test import independently → Full export-import loop works
4. Add User Story 3 → Test merge behavior → Power user workflow supported
5. Polish → Edge cases, re-sort persistence, localStorage checks
6. Each story adds value without breaking previous stories

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Constitution requires TDD: all test tasks (T002, T003, T006, T011, T015) MUST be written and fail before their corresponding implementation tasks
- JS changes are in inline `<script>` blocks within Razor pages (no separate .js files in this project)
- Commit after each phase completion for clean git history
- The C# `MergeSelections` method (T016) exists for testability — the actual merge happens in JS via the natural behavior of the import loop (T012/T017)
