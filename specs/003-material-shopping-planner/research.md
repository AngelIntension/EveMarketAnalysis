# Research: Material Shopping List Planner

**Branch**: `003-material-shopping-planner` | **Date**: 2026-03-15

## R1: Blueprint Bill-of-Materials Data Source

**Decision**: Reuse existing `BlueprintDataService` which already bundles the SDE as an embedded JSON resource (`Data/blueprints.json`).

**Rationale**: The `BlueprintDataService` is already a singleton that lazy-loads 1000+ blueprint activities from `blueprints.json`. Each `BlueprintActivity` contains `ProducedTypeId`, `ProducedQuantity`, `BaseTime`, and `ImmutableArray<MaterialRequirement>` with `TypeId` and base `Quantity`. This is exactly the SDE manufacturing data needed for material expansion. No new data source required.

**Alternatives considered**:
- Third-party API (Fuzzwork, EVERef): Rejected — adds runtime dependency and latency for static data already available locally.
- Raw SDE SQLite import: Rejected — `blueprints.json` already provides the subset needed; adding a full SDE database is unnecessary complexity.

## R2: Recursive Material Expansion Algorithm

**Decision**: Pure recursive function that walks the material tree, substituting intermediates with sub-materials when the character owns a blueprint for that intermediate. Each sub-blueprint's own ME is applied independently.

**Rationale**: The expansion must be a pure function taking `(blueprintTypeId, runs, ME, produceComponents, ownedBlueprintMap, visitedSet)` and returning an immutable `MaterialTreeNode`. The `visitedSet` parameter prevents circular dependencies (cycle detection). The `ownedBlueprintMap` is a `FrozenDictionary<int, CharacterBlueprint>` keyed by `ProducedTypeId` — mapping "what does this blueprint make?" to the character's owned blueprint for that product.

**Key design decisions**:
- **Mapping direction**: `BlueprintDataService` maps `blueprintTypeId → BlueprintActivity` (including `ProducedTypeId`). To check "do I own a blueprint for material X?", we need a reverse map: `producedTypeId → CharacterBlueprint`. Build this once on page load.
- **ME application**: `adjustedQuantity = max(1, ceil(baseQuantity * (1 - ME / 100.0)))` per material per blueprint. Each level in the recursive tree uses its own blueprint's ME.
- **Cycle detection**: Pass an `ImmutableHashSet<int>` of visited `producedTypeId` values down the recursion. If a material's producedTypeId is already in the set, treat it as a leaf (do not expand).
- **Aggregation**: After tree construction, flatten all leaf nodes and aggregate by `TypeId`, summing quantities. Track per-blueprint contribution for the detail drill-down.

**Alternatives considered**:
- Iterative BFS with mutable queue: Rejected — harder to reason about, violates immutable style constitution.
- Pre-computed full expansion stored in data: Rejected — expansion depends on character's owned blueprints which vary per user.

## R3: Market Cost Integration Pattern

**Decision**: Reuse existing `EsiMarketClient.GetMarketSnapshotAsync(regionId, typeId)` to fetch lowest sell order prices. Dispatch all material lookups in parallel with `SemaphoreSlim(20)` throttle.

**Rationale**: The `EsiMarketClient` already fetches orders and history, caches for 5 minutes, and returns `MarketSnapshot` with `LowestSellPrice`. The `ProfitabilityCalculator` already demonstrates the parallel-fetch-with-throttle pattern. The shopping list planner follows the same approach: collect unique `TypeId` values from the aggregated list, dispatch parallel market calls, map results back to shopping list items.

**Key design decisions**:
- Cost fetching is a separate operation from list generation (FR-012: region change must not recalculate quantities).
- The page exposes two JSON handlers: `OnGetShoppingListAsync` (generates list) and `OnGetCostsAsync` (fetches costs for current list items in a given region).
- Volume data (`m³`) is fetched alongside costs from `GET /universe/types/{typeId}` (already cached 24h in existing patterns).

**Alternatives considered**:
- Single handler that returns list + costs together: Rejected — violates FR-012 (region change must only refresh costs).
- Client-side market API calls: Rejected — ESI CORS restrictions; server-side proxying is the established pattern.

## R4: Owned Blueprint Map Construction

**Decision**: Build a `FrozenDictionary<int, CharacterBlueprint>` on page load, keyed by the `ProducedTypeId` of each owned blueprint (looked up via `BlueprintDataService`).

**Rationale**: To check "can I produce material X myself?", we need O(1) lookup from a material's typeId to the character's owned blueprint that produces it. `BlueprintDataService.GetBlueprintActivity(blueprint.TypeId)` returns the `BlueprintActivity` which contains `ProducedTypeId`. We invert this mapping once.

**Key design decisions**:
- If the character owns multiple blueprints that produce the same item (e.g., multiple BPCs), use the one with the highest ME for optimal material efficiency.
- Map is built once per page load and passed immutably to all expansion calls.
- `FrozenDictionary` chosen for thread-safe, zero-allocation lookups after construction.

## R5: Export Format

**Decision**: CSV export with columns: Material Name, Quantity, Category, Estimated Cost, Volume (m³). Copy-to-clipboard uses tab-separated format for paste compatibility with spreadsheets.

**Rationale**: CSV is universally supported. Tab-separated clipboard format pastes cleanly into Google Sheets, Excel, and in-game notepad. Cost and volume columns are included only if cost data has been loaded (otherwise omitted).

**Alternatives considered**:
- JSON export: Rejected — target audience (EVE industrialists) expects spreadsheet-compatible formats.
- EVE Multibuy format: Considered but deferred — could be added later as an enhancement.
