# Tasks: Manufacturing Profitability Calculator

**Input**: Design documents from `/specs/002-manufacturing-profitability/`
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, contracts/

**Tests**: Included per constitution (Principle III: TDD is NON-NEGOTIABLE). Tests are written FIRST and must FAIL before implementation.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization, SDE data file, ESI scope configuration

- [ ] T001 Add `esi-characters.read_blueprints.v1` to the configured scopes in EveMarketAnalysisClient/appsettings.json under the `Esi:Scopes` key
- [ ] T002 Create SDE data directory and bundled blueprints JSON file at EveMarketAnalysisClient/Data/blueprints.json with manufacturing activity data extracted from EVE SDE (schema: `{ "blueprintTypeId": { "producedTypeId", "producedQuantity", "time", "materials": [{ "typeId", "quantity" }] } }`) — include at least 10-20 representative blueprints for testing (e.g., Rifter=587, Thorax=24690, Catalyst=16240)
- [ ] T003 Configure EveMarketAnalysisClient/Data/blueprints.json as an embedded resource in EveMarketAnalysisClient/EveMarketAnalysisClient.csproj (add `<EmbeddedResource Include="Data\blueprints.json" />`)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core models, service interfaces, and shared services that ALL user stories depend on

**CRITICAL**: No user story work can begin until this phase is complete

### Models (all immutable records)

- [ ] T004 [P] Create CharacterBlueprint record in EveMarketAnalysisClient/Models/CharacterBlueprint.cs with fields: ItemId (long), TypeId (int), TypeName (string), MaterialEfficiency (int), TimeEfficiency (int), Runs (int), IsCopy (bool)
- [ ] T005 [P] Create BlueprintActivity record in EveMarketAnalysisClient/Models/BlueprintActivity.cs with fields: BlueprintTypeId (int), ProducedTypeId (int), ProducedQuantity (int), BaseTime (int), Materials (ImmutableArray\<MaterialRequirement\>)
- [ ] T006 [P] Create MaterialRequirement record in EveMarketAnalysisClient/Models/MaterialRequirement.cs with fields: TypeId (int), TypeName (string), BaseQuantity (int), AdjustedQuantity (int)
- [ ] T007 [P] Create MarketSnapshot record in EveMarketAnalysisClient/Models/MarketSnapshot.cs with fields: TypeId (int), RegionId (int), LowestSellPrice (decimal?), HighestBuyPrice (decimal?), AverageDailyVolume (double), FetchedAt (DateTimeOffset)
- [ ] T008 [P] Create TradeHubRegion record in EveMarketAnalysisClient/Models/TradeHubRegion.cs with fields: RegionId (int), RegionName (string), HubName (string), IsDefault (bool) — include static property `All` returning ImmutableArray of 5 trade hubs (The Forge/Jita, Domain/Amarr, Sinq Laison/Dodixie, Metropolis/Hek, Heimatar/Rens) and static property `Default` returning The Forge
- [ ] T009 [P] Create ProfitabilitySettings record in EveMarketAnalysisClient/Models/ProfitabilitySettings.cs with fields: RegionId (int, default 10000002), TaxRate (decimal, default 0.08m), InstallationFeeRate (decimal, default 0.01m)
- [ ] T010 [P] Create ProfitabilityResult record in EveMarketAnalysisClient/Models/ProfitabilityResult.cs with fields: Blueprint (CharacterBlueprint), ProducedTypeName (string), ProducedTypeId (int), Materials (ImmutableArray\<MaterialRequirement\>), TotalMaterialCost (decimal), ProductSellValue (decimal), TaxAmount (decimal), InstallationFee (decimal), GrossProfit (decimal), ProfitMarginPercent (double), ProductionTimeSeconds (int), IskPerHour (double), AverageDailyVolume (double), HasMarketData (bool), ErrorMessage (string?)
- [ ] T011 [P] Create ProfitabilityResponse record in EveMarketAnalysisClient/Models/ProfitabilityResponse.cs with fields: Results (ImmutableArray\<ProfitabilityResult\>), RegionId (int), RegionName (string), TaxRate (decimal), TotalBlueprints (int), SuccessCount (int), ErrorCount (int), FetchedAt (DateTimeOffset)

### Service Interfaces

- [ ] T012 [P] Create IEsiMarketClient interface in EveMarketAnalysisClient/Services/Interfaces/IEsiMarketClient.cs with methods: GetMarketSnapshotAsync(int regionId, int typeId) returning Task\<MarketSnapshot\>
- [ ] T013 [P] Create IBlueprintDataService interface in EveMarketAnalysisClient/Services/Interfaces/IBlueprintDataService.cs with methods: GetBlueprintActivity(int blueprintTypeId) returning BlueprintActivity?, GetAllBlueprintActivities() returning IReadOnlyDictionary\<int, BlueprintActivity\>
- [ ] T014 [P] Create IProfitabilityCalculator interface in EveMarketAnalysisClient/Services/Interfaces/IProfitabilityCalculator.cs with method: CalculateAsync(ImmutableArray\<CharacterBlueprint\> blueprints, ProfitabilitySettings settings) returning Task\<ImmutableArray\<ProfitabilityResult\>\>
- [ ] T015 Add GetCharacterBlueprintsAsync(int characterId) method signature to EveMarketAnalysisClient/Services/Interfaces/IEsiCharacterClient.cs returning Task\<ImmutableArray\<CharacterBlueprint\>\>

### Core Service Implementations

- [ ] T016 Implement BlueprintDataService in EveMarketAnalysisClient/Services/BlueprintDataService.cs — loads EveMarketAnalysisClient/Data/blueprints.json embedded resource on first access, deserializes into IReadOnlyDictionary\<int, BlueprintActivity\>, provides lookup by blueprint type ID. Use System.Text.Json for deserialization. Cache the parsed dictionary in a private field (loaded once, immutable).
- [ ] T017 Implement GetCharacterBlueprintsAsync in EveMarketAnalysisClient/Services/EsiCharacterClient.cs — call GET /characters/{character_id}/blueprints/ via Kiota client, map response to ImmutableArray\<CharacterBlueprint\>, resolve blueprint names via existing bulk POST /universe/names
- [ ] T018 Implement EsiMarketClient in EveMarketAnalysisClient/Services/EsiMarketClient.cs — fetches orders (GET /markets/{regionId}/orders?type_id={typeId}) and history (GET /markets/{regionId}/history?type_id={typeId}) via Kiota client, extracts lowest sell price and highest buy price from orders, computes 30-day average daily volume from history, returns MarketSnapshot record. Cache results in IMemoryCache with 5-minute TTL keyed by `market:{regionId}:{typeId}`. Handle ESI pagination via X-Pages header.

### DI Registration

- [ ] T019 Register new services in EveMarketAnalysisClient/Program.cs: IBlueprintDataService as singleton, IEsiMarketClient as scoped, IProfitabilityCalculator as scoped

**Checkpoint**: Foundation ready — all models, interfaces, and core services in place. User story implementation can now begin.

---

## Phase 3: User Story 3 — Authentication Gate (Priority: P1)

**Goal**: Ensure only authenticated users with valid ESI tokens can access the Manufacturing Profitability page.

**Independent Test**: Navigate to /ManufacturingProfitability without logging in and confirm redirect to /Auth/Login.

### Tests for User Story 3

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T020 [P] [US3] Write auth gate tests in EveMarketAnalysisClient.Tests/Pages/ManufacturingProfitabilityPageTests.cs — test that OnGet returns redirect when user is unauthenticated, test that OnGet succeeds when user has valid claims (character ID, name, access_token). Follow existing pattern from EveMarketAnalysisClient.Tests/Pages/CharacterSummaryPageTests.cs.

### Implementation for User Story 3

- [ ] T021 [US3] Create ManufacturingProfitability page model in EveMarketAnalysisClient/Pages/ManufacturingProfitability.cshtml.cs — add [Authorize] attribute, inject IProfitabilityCalculator and IEsiCharacterClient via constructor, implement OnGet() that extracts character ID and name from claims (mirror CharacterSummary pattern). Add stub OnGetProfitabilityAsync returning empty JsonResult (will be completed in US1).
- [ ] T022 [US3] Create minimal Razor page at EveMarketAnalysisClient/Pages/ManufacturingProfitability.cshtml — page title "Manufacturing Profitability", placeholder content "Loading...", basic layout matching existing EVE dark theme from _Layout.cshtml

**Checkpoint**: Unauthenticated users are redirected to login. Authenticated users see the page shell.

---

## Phase 4: User Story 2 — Profitability Calculation per Blueprint (Priority: P1)

**Goal**: Accurately calculate profitability for each blueprint using ME/TE adjustments, live market prices, and tax/fee estimates.

**Independent Test**: Verify that for a known blueprint (Rifter BPC, ME 10, TE 20) the calculated material cost, production time, sell value, and profit match hand-calculated expected values.

### Tests for User Story 2

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T023 [US2] Write ME formula unit tests in EveMarketAnalysisClient.Tests/Unit/ProfitabilityCalculatorTests.cs — test ME=0 returns base quantity, ME=10 applies max(1, ceil(base*(1-0.10))), ME=5 with small base quantity (base=2) returns max(1, ceil(2*0.95))=2, ME=10 with base=1 returns 1 (max guarantee). Use xUnit [Theory] with [InlineData] for multiple ME/base combinations.
- [ ] T024 [US2] Write TE formula unit tests in EveMarketAnalysisClient.Tests/Unit/ProfitabilityCalculatorTests.cs — test TE=0 returns base time, TE=20 applies base_time*(1-TimeEfficiency/100), verify production time in seconds. (Note: TE=20 gives 20% reduction: base_time * 0.80)
- [ ] T025 [US2] Write profit computation unit tests in EveMarketAnalysisClient.Tests/Unit/ProfitabilityCalculatorTests.cs — test gross profit = sell_value - material_cost - (sell_value * tax_rate) - (sell_value * install_fee_rate), test profit margin = (gross_profit / material_cost) * 100, test ISK/hour = gross_profit / (production_time_seconds / 3600.0), test with zero material cost returns 0 margin, test negative profit scenarios
- [ ] T026 [US2] Write sell value determination tests in EveMarketAnalysisClient.Tests/Unit/ProfitabilityCalculatorTests.cs — test uses highest buy price when available, test falls back to lowest sell price when no buy orders (HighestBuyPrice is null), test returns 0 when no market data at all
- [ ] T027 [P] [US2] Write BlueprintDataService unit tests in EveMarketAnalysisClient.Tests/Unit/BlueprintDataServiceTests.cs — test loading embedded JSON returns expected activities, test lookup by valid blueprint type ID returns BlueprintActivity, test lookup by unknown type ID returns null, test GetAllBlueprintActivities returns non-empty dictionary
- [ ] T028 [P] [US2] Write EsiMarketClient tests in EveMarketAnalysisClient.Tests/Services/EsiMarketClientTests.cs — test GetMarketSnapshotAsync returns lowest sell and highest buy from mock orders, test caching (second call with same regionId/typeId returns cached result without ESI call), test handles empty order list (returns null prices), test 30-day average volume calculation from mock history data

### Implementation for User Story 2

- [ ] T029 [US2] Implement ProfitabilityCalculator in EveMarketAnalysisClient/Services/ProfitabilityCalculator.cs — pure calculation logic: (1) for each blueprint, look up BlueprintActivity from IBlueprintDataService, (2) compute adjusted material quantities using ME formula: max(1, ceil(base_quantity * (1 - ME/100))), (3) compute production time: base_time * (1 - TE/100), (4) collect all unique type IDs (materials + product), (5) fetch MarketSnapshots for all types in parallel via IEsiMarketClient with SemaphoreSlim(20) concurrency limiter and Task.WhenAll, (6) compute total material cost = sum(adjusted_qty * lowest_sell_price), (7) determine sell value = highest buy price ?? lowest sell price, (8) compute tax = sell_value * tax_rate, install_fee = sell_value * install_fee_rate, (9) gross_profit = sell_value - material_cost - tax - install_fee, (10) margin = gross_profit / material_cost * 100, (11) ISK/hour = gross_profit / (production_time / 3600.0), (12) return ImmutableArray sorted by ISK/hour descending, limited to top 50. Handle missing SDE data and missing market data gracefully per result.
- [ ] T030 [US2] Resolve type names for materials and products — use existing bulk POST /universe/names pattern from EsiCharacterClient to resolve all unique type IDs to display names, cache with 24h TTL (follow existing name resolution pattern)

**Checkpoint**: ProfitabilityCalculator can compute accurate profitability for any blueprint with mocked market data. All formula tests pass.

---

## Phase 5: User Story 1 — View Ranked Profitability Table (Priority: P1) MVP

**Goal**: Display a sortable table of the top 50 most profitable manufacturing items, loaded asynchronously with skeleton placeholders.

**Independent Test**: Log in with a character that owns blueprints, navigate to /ManufacturingProfitability, verify a sorted table appears with item names, profit margins, ISK/hour, daily volume, and materials.

### Tests for User Story 1

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T031 [P] [US1] Write OnGetProfitabilityAsync handler tests in EveMarketAnalysisClient.Tests/Pages/ManufacturingProfitabilityPageTests.cs — test returns JsonResult with ProfitabilityResponse, test passes regionId and taxRate to ProfitabilityCalculator, test returns 400 for invalid regionId, test returns 400 for taxRate outside 0.0-1.0, test returns results sorted by ISK/hour descending limited to 50
- [ ] T032 [P] [US1] Write integration test in EveMarketAnalysisClient.Tests/Services/ProfitabilityIntegrationTests.cs — test end-to-end flow: mock EsiCharacterClient returning blueprints, mock EsiMarketClient returning market snapshots, inject real BlueprintDataService (from test SDE JSON) and real ProfitabilityCalculator, verify results contain expected item names, correct profit calculations, correct sort order

### Implementation for User Story 1

- [ ] T033 [US1] Complete OnGetProfitabilityAsync handler in EveMarketAnalysisClient/Pages/ManufacturingProfitability.cshtml.cs — accept regionId (int, default 10000002) and taxRate (decimal, default 0.08) query parameters, validate regionId is one of the 5 trade hubs and taxRate is 0.0-1.0, call IEsiCharacterClient.GetCharacterBlueprintsAsync(characterId), create ProfitabilitySettings, call IProfitabilityCalculator.CalculateAsync, build ProfitabilityResponse, return JsonResult. Handle errors with appropriate status codes.
- [ ] T034 [US1] Build full Razor page UI in EveMarketAnalysisClient/Pages/ManufacturingProfitability.cshtml — EVE dark theme matching existing pages, skeleton loading placeholders (animated per CharacterSummary pattern), sortable table with columns: Item Name, ME/TE, BPO/BPC indicator, Profit Margin %, ISK/Hour, Daily Volume, Materials Summary. Default sort ISK/hour descending. Top 50 limit. JavaScript fetch to OnGetProfitabilityAsync on page load. Client-side column sort on header click (sortable: profit %, ISK/hour, daily volume). Use esc() helper for XSS protection. Include region selector dropdown and tax rate input field (wired up in US4).
- [ ] T035 [US1] Add navigation link to ManufacturingProfitability page in EveMarketAnalysisClient/Pages/Shared/_Layout.cshtml — add nav item visible only when authenticated (mirror existing CharacterSummary nav link pattern)

**Checkpoint**: MVP complete. Authenticated users see a sortable profitability table with skeleton loading. The Forge (default) market data is used. Tax rate is fixed at 8% default.

---

## Phase 6: User Story 4 — Region Selection (Priority: P2)

**Goal**: Allow users to select a trade hub region and recalculate profitability with that region's market data.

**Independent Test**: Select Domain (Amarr) from the region selector and verify all prices, profits, and volumes update to reflect Amarr market data.

### Tests for User Story 4

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T036 [P] [US4] Write TradeHubRegion unit tests in EveMarketAnalysisClient.Tests/Unit/TradeHubRegionTests.cs — test All returns exactly 5 regions, test Default returns The Forge, test all region IDs match expected values (10000002, 10000043, 10000032, 10000042, 10000030), test IsDefault is true only for The Forge
- [ ] T037 [P] [US4] Write region parameter handler tests in EveMarketAnalysisClient.Tests/Pages/ManufacturingProfitabilityPageTests.cs — test OnGetProfitabilityAsync passes correct regionId to calculator, test invalid regionId (99999) returns 400, test default regionId is 10000002 when not specified

### Implementation for User Story 4

- [ ] T038 [US4] Wire region selector in ManufacturingProfitability.cshtml — populate dropdown from TradeHubRegion.All, default to The Forge, on change trigger JavaScript refetch with selected regionId parameter. Display selected region name in results header. Persist selection in the page's JavaScript state for the session.
- [ ] T039 [US4] Wire tax rate input in ManufacturingProfitability.cshtml — numeric input field defaulting to 8, on change trigger JavaScript refetch with taxRate parameter (convert from percentage to decimal: input/100). Validate client-side that value is 0-100. Persist in JavaScript state.

**Checkpoint**: Users can switch regions and adjust tax rate. Results recalculate with new parameters.

---

## Phase 7: User Story 5 — Graceful Error Handling (Priority: P2)

**Goal**: Show clear, user-friendly messages for all error conditions instead of raw errors or blank screens.

**Independent Test**: Simulate each error condition (no blueprints, ESI errors, missing market data) and verify appropriate messages appear.

### Tests for User Story 5

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [ ] T040 [P] [US5] Write error handling tests in EveMarketAnalysisClient.Tests/Pages/ManufacturingProfitabilityPageTests.cs — test OnGetProfitabilityAsync returns appropriate response when character has zero blueprints (empty results with message), test returns 500 with error message when EsiCharacterClient throws, test partial failure (some results succeed, some have ErrorMessage set)
- [ ] T041 [P] [US5] Write calculator error handling tests in EveMarketAnalysisClient.Tests/Unit/ProfitabilityCalculatorTests.cs — test blueprint with no SDE match sets HasMarketData=false and ErrorMessage, test blueprint with no market orders sets HasMarketData=false, test partial material failure (some materials have prices, some don't) sets appropriate ErrorMessage

### Implementation for User Story 5

- [ ] T042 [US5] Add error state UI to ManufacturingProfitability.cshtml — "No blueprints found" message with guidance when results are empty, "ESI unavailable" banner when fetch fails (HTTP 500 response), per-row "No market data" / "Materials unavailable in [region]" indicators for individual failures, error/success count summary in response header. Style error states with EVE theme (red/amber indicators).
- [ ] T043 [US5] Add SafeAsync pattern to ProfitabilityCalculator — wrap per-blueprint calculation in try/catch to prevent single blueprint failure from blocking entire batch (follow existing SafeAsync pattern from CharacterService), log warnings for skipped blueprints, set ErrorMessage on failed ProfitabilityResult records

**Checkpoint**: All error conditions display user-friendly messages. Partial failures don't break the table.

---

## Phase 8: Polish & Cross-Cutting Concerns

**Purpose**: Final validation, build verification, and cleanup

- [ ] T044 Verify all tests pass by running `dotnet test EveMarketAnalysisClient.Tests` — fix any failures
- [ ] T045 Verify build succeeds by running `dotnet build EveMarketAnalysis.sln` — fix any compiler errors or warnings
- [ ] T046 Run quickstart validation — start app with `dotnet run --project EveMarketAnalysisClient --launch-profile https`, navigate to https://localhost:7272/ManufacturingProfitability, verify page loads and redirects to login when unauthenticated

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion — BLOCKS all user stories
- **US3 Auth Gate (Phase 3)**: Depends on Foundational — can start after Phase 2
- **US2 Calculation (Phase 4)**: Depends on Foundational — can start after Phase 2, can run in parallel with US3
- **US1 Table (Phase 5)**: Depends on US3 (page exists) and US2 (calculator exists)
- **US4 Region (Phase 6)**: Depends on US1 (page and handler exist)
- **US5 Errors (Phase 7)**: Depends on US2 (calculator) and US1 (page UI)
- **Polish (Phase 8)**: Depends on all desired user stories being complete

### User Story Dependencies

```
Phase 1 (Setup) → Phase 2 (Foundation)
                    ├──► US3 (Auth) ──────────────┐
                    └──► US2 (Calculation) ────────┤
                                                   ├──► US1 (Table/MVP) ──► US4 (Region)
                                                   │                    ──► US5 (Errors)
                                                   └──────────────────────► Polish
```

- **US3 (Auth)**: Independent after Foundation — no dependency on other stories
- **US2 (Calculation)**: Independent after Foundation — no dependency on other stories
- **US1 (Table)**: Depends on US3 (page model) + US2 (calculator service)
- **US4 (Region)**: Depends on US1 (UI to extend)
- **US5 (Errors)**: Depends on US1 (UI) + US2 (calculator to add SafeAsync)

### Within Each User Story

- Tests MUST be written and FAIL before implementation
- Models before services
- Services before page handlers
- Core implementation before integration

### Parallel Opportunities

- **Phase 2**: All model records (T004-T011) can be created in parallel. All interfaces (T012-T015) in parallel.
- **Phase 3 + Phase 4**: US3 and US2 can run in parallel after Foundation
- **Within US2**: T023-T026 share a file (sequential), T027-T028 are independent (parallel with each other and with T023-T026 block)
- **Within US1**: Both test tasks (T031-T032) can run in parallel
- **Within US4**: Both test tasks (T036-T037) can run in parallel
- **Within US5**: Both test tasks (T040-T041) can run in parallel

---

## Parallel Example: Foundation Models

```
# Launch all model record tasks together (all different files, no dependencies):
T004: Create CharacterBlueprint record in EveMarketAnalysisClient/Models/CharacterBlueprint.cs
T005: Create BlueprintActivity record in EveMarketAnalysisClient/Models/BlueprintActivity.cs
T006: Create MaterialRequirement record in EveMarketAnalysisClient/Models/MaterialRequirement.cs
T007: Create MarketSnapshot record in EveMarketAnalysisClient/Models/MarketSnapshot.cs
T008: Create TradeHubRegion record in EveMarketAnalysisClient/Models/TradeHubRegion.cs
T009: Create ProfitabilitySettings record in EveMarketAnalysisClient/Models/ProfitabilitySettings.cs
T010: Create ProfitabilityResult record in EveMarketAnalysisClient/Models/ProfitabilityResult.cs
T011: Create ProfitabilityResponse record in EveMarketAnalysisClient/Models/ProfitabilityResponse.cs
```

## Parallel Example: US2 Tests

```
# T023-T026 target the same file (ProfitabilityCalculatorTests.cs) — run sequentially:
T023: ME formula unit tests in ProfitabilityCalculatorTests.cs
T024: TE formula unit tests in ProfitabilityCalculatorTests.cs
T025: Profit computation unit tests in ProfitabilityCalculatorTests.cs
T026: Sell value determination tests in ProfitabilityCalculatorTests.cs

# T027-T028 target different files — can run in parallel with each other and with T023-T026:
T027: BlueprintDataService unit tests in BlueprintDataServiceTests.cs
T028: EsiMarketClient tests in EsiMarketClientTests.cs
```

---

## Implementation Strategy

### MVP First (User Stories 3 + 2 + 1)

1. Complete Phase 1: Setup (T001-T003)
2. Complete Phase 2: Foundational (T004-T019)
3. Complete Phase 3: US3 Auth Gate (T020-T022)
4. Complete Phase 4: US2 Calculation (T023-T030)
5. Complete Phase 5: US1 Table (T031-T035)
6. **STOP and VALIDATE**: Test the full MVP independently — log in, navigate to page, verify profitability table loads with correct data
7. Deploy/demo if ready

### Incremental Delivery

1. Setup + Foundational → Foundation ready
2. Add US3 (Auth) → Auth gate working
3. Add US2 (Calculation) → Pure calculation logic tested
4. Add US1 (Table) → **MVP complete** — full profitability table visible
5. Add US4 (Region) → Region selection working
6. Add US5 (Errors) → All error states handled gracefully
7. Polish → Navigation, build verification

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Constitution requires TDD: tests are written FIRST and must FAIL before implementation
- All models are immutable records with ImmutableArray collections
- All independent ESI calls use Task.WhenAll with concurrency limiting
- Market data cached via IMemoryCache with 5-minute TTL
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
