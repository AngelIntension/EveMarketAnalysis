# Data Model: Blueprint Portfolio Optimizer

**Branch**: `004-blueprint-portfolio-optimizer` | **Date**: 2026-03-15

## New Records

### PhaseDefinition

Static phase definition loaded from `phases.json`.

```
PhaseDefinition
├── PhaseNumber         : int (1–5)
├── Name                : string ("T1 Frigate Foundation", etc.)
├── Description         : string (human-readable phase description)
└── CandidateTypeIds    : ImmutableArray<int> (blueprint type IDs in this phase)
```

- Loaded once at startup (singleton).
- Immutable after load.
- Type IDs reference blueprint type IDs from `blueprints.json`.

### PhaseStatus

Runtime phase evaluation result.

```
PhaseStatus
├── Phase               : PhaseDefinition
├── OwnedProfitableCount : int (blueprints meeting ISK/hr threshold)
├── RequiredCount        : int (ceil(N × 9/11) where N = configured slot count)
├── IsComplete           : bool (OwnedProfitableCount >= RequiredCount)
├── DailyPotentialIncome : decimal (sum of ISK/hr × 24 for all profitable BPs in phase)
└── CompletionReason     : string? ("slots" | "income" | "manual" | null)
```

- Computed during refresh. Never cached.
- Pure derivation from owned blueprints + configuration.

### PortfolioConfiguration

User-configurable thresholds. Serialized to/from browser local storage (JSON).

```
PortfolioConfiguration
├── ProcurementStationId  : long (default: 60003760 = Jita 4-4)
├── ProcurementRegionId   : int (default: 10000002 = The Forge)
├── SellingHubStationId   : long (default: 60003760 = Jita 4-4)
├── SellingHubRegionId    : int (default: 10000002 = The Forge)
├── ManufacturingSystemId : int (default: 30000142 = Jita system)
├── BuyingBrokerFeePercent : decimal (default: 3.0)
├── SellingBrokerFeePercent : decimal (default: 3.0)
├── SalesTaxPercent        : decimal (default: 3.6)
├── MinIskPerHour          : decimal (default: 25_000_000)
├── DailyIncomeGoal        : decimal (default: 750_000_000)
├── ManufacturingSlots     : int (default: 11)
├── WhatIfME               : int? (null = use actual ME; 0-10 for what-if)
└── WhatIfTE               : int? (null = use actual TE; 0-20 for what-if)
```

- Sent from client JS as query parameters on Refresh.
- Server-side record is created from request parameters (never persisted server-side).
- Local storage key: `portfolioConfig`.

### BlueprintRankingEntry

Enriched blueprint for the ranking table.

```
BlueprintRankingEntry
├── Blueprint              : CharacterBlueprint (existing record)
├── ProducedTypeName       : string
├── ProducedTypeId         : int
├── PhaseNumber            : int? (which phase this BP belongs to, null if none)
├── MaterialCost           : decimal (total material cost at procurement station buy prices)
├── ProductRevenue         : decimal (product value at selling hub sell prices)
├── BuyingBrokerFee        : decimal
├── SellingBrokerFee       : decimal
├── SalesTax               : decimal
├── SystemCostFee          : decimal (cost index × estimated item value)
├── GrossProfit            : decimal (revenue - costs - fees - taxes)
├── ProfitMarginPercent    : decimal
├── ProductionTimeSeconds  : double
├── IskPerHour             : decimal
├── AverageDailyVolume     : double
├── IsCurrentPhase         : bool
├── MeetsThreshold         : bool (ISK/hr >= configured minimum)
├── HasMarketData          : bool
└── ErrorMessage           : string?
```

- Computed per blueprint during refresh.
- Sorted by IskPerHour descending.
- Records with errors sort to the bottom.

### BpoPurchaseRecommendation

Unowned BPO from the current (or next) phase.

```
BpoPurchaseRecommendation
├── BlueprintTypeId        : int
├── BlueprintName          : string
├── ProducedTypeName       : string
├── PhaseNumber            : int
├── NpcSeededPrice         : decimal? (from ESI adjusted_price)
├── PlayerMarketPrice      : decimal? (lowest sell region-wide)
├── ProjectedIskPerHour    : decimal (at ME10/TE20)
├── PaybackPeriodDays      : decimal? (buy price / daily profit)
├── RoiPercent             : decimal? ((daily profit × 30) / buy price × 100)
├── HasMarketData          : bool
└── ErrorMessage           : string?
```

- Computed from phase type IDs minus owned blueprint type IDs.
- Sorted by ProjectedIskPerHour descending.

### ResearchRecommendation

Owned blueprint that could benefit from more ME/TE research.

```
ResearchRecommendation
├── Blueprint              : CharacterBlueprint
├── ProducedTypeName       : string
├── CurrentIskPerHour      : decimal (at current ME/TE)
├── ProjectedIskPerHour    : decimal (at ME10/TE20)
├── IskPerHourGain         : decimal (projected - current)
├── GainPercent            : decimal
├── CurrentME              : int
├── CurrentTE              : int
├── TargetME               : int (10)
└── TargetTE               : int (20)
```

- Only includes blueprints where ME < 10 or TE < 20.
- Sorted by IskPerHourGain descending.
- Capped at 10 results.

### PortfolioAnalysis

Top-level result returned to the page.

```
PortfolioAnalysis
├── Rankings               : ImmutableArray<BlueprintRankingEntry>
├── PhaseStatuses          : ImmutableArray<PhaseStatus>
├── CurrentPhaseNumber     : int (1–5)
├── PhaseOverrideActive    : bool
├── BpoRecommendations     : ImmutableArray<BpoPurchaseRecommendation>
├── ResearchRecommendations : ImmutableArray<ResearchRecommendation>
├── TotalBlueprintsEvaluated : int
├── SuccessCount           : int
├── ErrorCount             : int
├── PortfolioSizeWarning   : bool (> 300 blueprints)
└── FetchedAt              : DateTimeOffset
```

- Returned as JSON from the AJAX handler.
- Immutable; entire structure is computed fresh on each Refresh.

## Extended Existing Records

### EsiMarketClient (new method)

```
GetRegionMarketSnapshotAsync(regionId: int, typeId: int) → MarketSnapshot
```

- Same as existing `GetMarketSnapshotAsync` but does NOT filter to trade hub station.
- Returns lowest sell and highest buy across the entire region.
- Used only for BPO purchase price lookups.

## Relationships

```
PortfolioConfiguration ──configures──> PortfolioAnalyzer
CharacterBlueprint[] ──input──> PortfolioAnalyzer
PhaseDefinition[] ──loaded by──> PhaseService (singleton)

PortfolioAnalyzer ──produces──> PortfolioAnalysis
  ├── BlueprintRankingEntry[] (one per owned blueprint)
  ├── PhaseStatus[] (one per phase, always 5)
  ├── BpoPurchaseRecommendation[] (unowned BPOs from recommendation phase)
  └── ResearchRecommendation[] (owned under-researched, max 10)

BlueprintRankingEntry ──references──> PhaseDefinition (via PhaseNumber)
BpoPurchaseRecommendation ──references──> PhaseDefinition (via PhaseNumber)
```

## State Transitions

### Phase Advancement

```
Phase N (incomplete) ──slot trigger──> Phase N (complete) ──auto-advance──> Phase N+1 (current)
Phase N (incomplete) ──income trigger──> Phase N (complete) ──auto-advance──> Phase N+1 (current)
Phase N (any) ──manual override──> Phase N+1 (current, override persisted)
Phase 5 (complete) ──terminal──> No further advancement
```

- Phase advancement is evaluated on each Refresh.
- Manual override is read from query parameters (originally from local storage).
- Override persists until cleared by user or superseded by auto-calculated phase.

## Validation Rules

- `PortfolioConfiguration.ManufacturingSlots` must be 1–50.
- `PortfolioConfiguration.MinIskPerHour` must be ≥ 0.
- `PortfolioConfiguration.DailyIncomeGoal` must be ≥ 0.
- Broker fee and tax percentages must be 0.0–100.0.
- `WhatIfME` must be 0–10 if provided.
- `WhatIfTE` must be 0–20 if provided.
- Station/region IDs must match a known `TradeHubRegion`.
