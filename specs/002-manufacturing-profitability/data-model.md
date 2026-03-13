# Data Model: Manufacturing Profitability Calculator

**Branch**: `002-manufacturing-profitability` | **Date**: 2026-03-13

## Entity Relationship Overview

```
CharacterBlueprint ──┐
                     ├──► BlueprintActivity (from SDE) ──► MaterialRequirement[]
                     │
                     └──► ProfitabilityResult
                            ├── MaterialCost (from MarketSnapshot[])
                            ├── ProductValue (from MarketSnapshot)
                            ├── TaxAndFees
                            └── DerivedMetrics (profit, margin%, ISK/hr)

MarketSnapshot ◄── ESI /markets/{region_id}/orders + /history
TradeHubRegion ◄── Static lookup (5 entries)
ProfitabilitySettings ◄── User input (tax rate, region)
```

## Entities

### CharacterBlueprint

Represents a single blueprint owned by the authenticated character, as returned by ESI.

| Field | Type | Description |
|-------|------|-------------|
| ItemId | long | Unique item instance ID |
| TypeId | int | Blueprint type identifier |
| TypeName | string | Resolved blueprint name |
| MaterialEfficiency | int | ME level (0-10) |
| TimeEfficiency | int | TE level (0-20) |
| Runs | int | Remaining runs (-1 for BPOs) |
| IsCopy | bool | True if BPC, false if BPO |

**Source**: `GET /characters/{character_id}/blueprints/`
**Identity**: ItemId (unique per item instance)
**Validation**: ME must be 0-10, TE must be 0-20

### BlueprintActivity

Static data mapping a blueprint type to its manufacturing activity (what it produces and what materials it requires). Loaded from bundled SDE JSON.

| Field | Type | Description |
|-------|------|-------------|
| BlueprintTypeId | int | Blueprint type identifier |
| ProducedTypeId | int | Type ID of the manufactured item |
| ProducedQuantity | int | Items produced per run (usually 1) |
| BaseTime | int | Base manufacturing time in seconds |
| Materials | MaterialRequirement[] | Required input materials |

**Source**: Bundled SDE JSON file (`sde/blueprints.json`)
**Identity**: BlueprintTypeId
**Lifecycle**: Static — only changes with game patches

### MaterialRequirement

A single material required for a manufacturing job, before ME adjustments.

| Field | Type | Description |
|-------|------|-------------|
| TypeId | int | Material type identifier |
| TypeName | string | Resolved material name |
| BaseQuantity | int | Base quantity per run (before ME) |
| AdjustedQuantity | int | Quantity after ME reduction (computed) |

**Computed field**: `AdjustedQuantity = max(1, ceil(BaseQuantity * (1 - ME/100)))`

### MarketSnapshot

A point-in-time capture of market conditions for a specific item type in a specific region.

| Field | Type | Description |
|-------|------|-------------|
| TypeId | int | Item type identifier |
| RegionId | int | Region identifier |
| LowestSellPrice | decimal? | Best (lowest) sell order price, null if no orders |
| HighestBuyPrice | decimal? | Best (highest) buy order price, null if no orders |
| AverageDailyVolume | double | Average daily trade volume (30-day average) |
| FetchedAt | DateTimeOffset | When this snapshot was captured |

**Source**: `GET /markets/{region_id}/orders/?type_id={type_id}` + `GET /markets/{region_id}/history/?type_id={type_id}`
**Identity**: (TypeId, RegionId)
**Cache key**: `market:orders:{RegionId}:{TypeId}` (5-minute TTL)

### TradeHubRegion

Static definition of a supported trade hub region for the region selector.

| Field | Type | Description |
|-------|------|-------------|
| RegionId | int | ESI region identifier |
| RegionName | string | Display name (e.g., "The Forge") |
| HubName | string | Trade hub name (e.g., "Jita") |
| IsDefault | bool | True for The Forge |

**Source**: Hard-coded static collection (5 entries)
**Lifecycle**: Static — these are permanent game regions

### ProfitabilitySettings

User-adjustable parameters for profitability calculations, persisted per session.

| Field | Type | Description |
|-------|------|-------------|
| RegionId | int | Selected trade hub region (default: 10000002) |
| TaxRate | decimal | Combined broker/transaction tax rate (default: 0.08) |
| InstallationFeeRate | decimal | Job installation fee rate (fixed: 0.01) |

**Lifecycle**: Session-scoped, not persisted beyond the browser session

### ProfitabilityResult

The computed profitability for a single blueprint, combining all inputs.

| Field | Type | Description |
|-------|------|-------------|
| Blueprint | CharacterBlueprint | Source blueprint |
| ProducedTypeName | string | Name of the manufactured item |
| ProducedTypeId | int | Type ID of the manufactured item |
| Materials | MaterialRequirement[] | Adjusted material list with quantities |
| TotalMaterialCost | decimal | Sum of (material price * adjusted quantity) |
| ProductSellValue | decimal | Estimated sell value of produced item |
| TaxAmount | decimal | Broker/transaction tax on sell value |
| InstallationFee | decimal | Estimated job installation fee |
| GrossProfit | decimal | SellValue - MaterialCost - Tax - Fee |
| ProfitMarginPercent | double | (GrossProfit / MaterialCost) * 100 |
| ProductionTimeSeconds | int | Adjusted production time in seconds |
| IskPerHour | double | GrossProfit / (ProductionTime in hours) |
| AverageDailyVolume | double | 30-day average daily trade volume |
| HasMarketData | bool | False if market data unavailable |
| ErrorMessage | string? | Error description if calculation failed |

**Identity**: (Blueprint.ItemId)
**Computed from**: CharacterBlueprint + BlueprintActivity + MarketSnapshot[] + ProfitabilitySettings

## State Transitions

### ProfitabilityResult Calculation States

```
Pending → Calculating → Success
                     → PartialFailure (some materials missing market data)
                     → NoMarketData (product has no orders)
                     → Error (ESI failure, unknown blueprint type)
```

- **Pending**: Blueprint fetched, awaiting market data
- **Calculating**: Market data being fetched and profitability computed
- **Success**: All data available, full calculation complete
- **PartialFailure**: Product has market data but some materials don't — cost shown as minimum estimate
- **NoMarketData**: Product type has no market orders in selected region
- **Error**: Unrecoverable error (SDE lookup failure, ESI error)

## Data Volume Estimates

- **Blueprints per character**: Typically 10-200, up to ~1000 for industrialists
- **Unique material types per blueprint set**: ~50-300 unique type_ids
- **Market orders per type per region**: 1-500 orders (usually < 100)
- **Market history entries per type**: ~365 entries (1 year of daily data)
- **SDE blueprint entries**: ~12,000 total manufacturing blueprints
- **Bundled SDE JSON size**: ~2-5 MB estimated
