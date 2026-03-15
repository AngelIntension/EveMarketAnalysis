# Data Model: Material Shopping List Planner

**Branch**: `003-material-shopping-planner` | **Date**: 2026-03-15

## New Entities

### BlueprintSelection

Represents a user's choice of one blueprint with production parameters.

| Field | Type | Description |
|-------|------|-------------|
| BlueprintTypeId | int | EVE type ID of the blueprint |
| BlueprintName | string | Display name of the blueprint |
| MaterialEfficiency | int | ME level (0-10) from character's owned blueprint |
| TimeEfficiency | int | TE level (0-20) from character's owned blueprint |
| Runs | int | Number of production runs requested by user |
| MaxRuns | int | Maximum runs available (-1 for BPO = unlimited) |
| IsCopy | bool | True if BPC, false if BPO |
| ProduceComponents | bool | True if recursive expansion is enabled |
| ProducedTypeId | int | What this blueprint manufactures (from SDE) |

**Relationships**: References a `CharacterBlueprint` (existing) and a `BlueprintActivity` (existing) via `BlueprintTypeId`.

**Validation**:
- `Runs` must be >= 1
- If `IsCopy` and `MaxRuns > 0`: `Runs` must be <= `MaxRuns`
- `MaterialEfficiency` must be 0-10
- `TimeEfficiency` must be 0-20

---

### MaterialTreeNode

Recursive structure representing material breakdown for one blueprint selection.

| Field | Type | Description |
|-------|------|-------------|
| TypeId | int | EVE type ID of this material |
| TypeName | string | Display name (resolved via bulk names) |
| BaseQuantity | int | Base quantity from SDE before ME adjustment |
| AdjustedQuantity | int | Quantity after ME: `max(1, ceil(base * (1 - ME/100)))` |
| Runs | int | Number of runs (multiplies adjusted quantity) |
| TotalQuantity | long | `AdjustedQuantity * Runs` |
| IsExpanded | bool | True if this node was recursively expanded |
| SourceBlueprintTypeId | int | Blueprint that requires this material |
| Children | ImmutableArray\<MaterialTreeNode\> | Sub-materials if expanded; empty if leaf |

**Relationships**: Self-referential tree. Leaf nodes are raw materials to purchase. Non-leaf nodes are intermediates expanded via owned blueprints.

**State transitions**: None — immutable after construction.

**Invariants**:
- If `IsExpanded` is true, `Children` must not be empty
- If `IsExpanded` is false, `Children` must be empty
- `TotalQuantity` = `AdjustedQuantity * Runs`

---

### ShoppingListItem

One aggregated row in the final shopping list output.

| Field | Type | Description |
|-------|------|-------------|
| TypeId | int | EVE type ID of the material |
| TypeName | string | Display name |
| Category | string | Market group / category name for grouping |
| TotalQuantity | long | Summed quantity across all contributing blueprints |
| Volume | double | Per-unit volume in m³ |
| TotalVolume | double | `TotalQuantity * Volume` |
| EstimatedUnitCost | decimal? | Lowest sell order price (null if not yet fetched) |
| EstimatedTotalCost | decimal? | `TotalQuantity * EstimatedUnitCost` (null if not yet fetched) |
| Sources | ImmutableArray\<MaterialSource\> | Breakdown of contributing blueprints |

**Relationships**: Aggregated from multiple `MaterialTreeNode` leaf nodes. Contains `MaterialSource` entries for drill-down.

---

### MaterialSource

Per-blueprint contribution to a shopping list item (for detail drill-down).

| Field | Type | Description |
|-------|------|-------------|
| BlueprintName | string | Name of the contributing blueprint |
| BlueprintTypeId | int | Type ID of the contributing blueprint |
| Quantity | long | How much of this material this blueprint requires |

---

### ShoppingListResponse

JSON response envelope returned by the shopping list handler.

| Field | Type | Description |
|-------|------|-------------|
| Items | ImmutableArray\<ShoppingListItem\> | Aggregated material rows |
| TotalEstimatedCost | decimal? | Sum of all item costs (null if costs not loaded) |
| TotalVolume | double | Sum of all item volumes |
| BlueprintCount | int | Number of blueprints included in this list |
| GeneratedAt | DateTimeOffset | Timestamp of list generation |
| Errors | ImmutableArray\<string\> | Per-blueprint error messages (graceful degradation) |

---

## Existing Entities (Reused)

### CharacterBlueprint (existing, no changes)

| Field | Type | Description |
|-------|------|-------------|
| ItemId | long | Unique asset ID |
| TypeId | int | Blueprint type ID |
| TypeName | string | Blueprint name |
| MaterialEfficiency | int | ME level |
| TimeEfficiency | int | TE level |
| Runs | int | -1 for BPO, positive for BPC |
| IsCopy | bool | True if BPC |

### BlueprintActivity (existing, no changes)

| Field | Type | Description |
|-------|------|-------------|
| BlueprintTypeId | int | Blueprint type ID (key in SDE) |
| ProducedTypeId | int | What it manufactures |
| ProducedQuantity | int | Units produced per run |
| BaseTime | int | Manufacturing time in seconds |
| Materials | ImmutableArray\<MaterialRequirement\> | Base materials (TypeId + Quantity) |

### MarketSnapshot (existing, no changes)

| Field | Type | Description |
|-------|------|-------------|
| TypeId | int | Item type ID |
| RegionId | int | Market region |
| LowestSellPrice | decimal? | Cheapest sell order |
| HighestBuyPrice | decimal? | Best buy order |
| AverageDailyVolume | double | 30-day average |
| FetchedAt | DateTimeOffset | Cache timestamp |

### TradeHubRegion (existing, no changes)

Static enumeration of 5 trade hubs. Default: The Forge / Jita (10000002).

## Key Data Flows

1. **Page Load**: `EsiCharacterClient.GetCharacterBlueprintsAsync()` → `ImmutableArray<CharacterBlueprint>` (cached)
2. **Build Owned Map**: For each `CharacterBlueprint`, look up `BlueprintDataService.GetBlueprintActivity(typeId)` → extract `ProducedTypeId` → build `FrozenDictionary<int, CharacterBlueprint>` (producedTypeId → blueprint with highest ME)
3. **Generate List**: For each `BlueprintSelection`, call `ExpandBlueprintToMaterials(selection, ownedMap, visitedSet)` → `MaterialTreeNode` tree
4. **Aggregate**: Flatten all trees → group by `TypeId` → sum quantities → `ImmutableArray<ShoppingListItem>`
5. **Fetch Costs**: For each unique `TypeId` in list → `EsiMarketClient.GetMarketSnapshotAsync(regionId, typeId)` in parallel → update `EstimatedUnitCost` and `EstimatedTotalCost` via `with`-expressions
6. **Fetch Volumes**: For each unique `TypeId` in list → `GET /universe/types/{typeId}` in parallel (cached 24h) → update `Volume` and `TotalVolume` via `with`-expressions
