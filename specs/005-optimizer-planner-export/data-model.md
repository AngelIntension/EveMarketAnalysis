# Data Model: Optimizer-to-Planner Export Integration

**Date**: 2026-03-16 | **Branch**: `005-optimizer-planner-export`

## Entities

### ProductionBatchExport

The top-level export payload stored in localStorage. Contains a list of blueprint run specifications for transfer from the optimizer to the planner.

| Field | Type | Description | Constraints |
|-------|------|-------------|-------------|
| Items | ImmutableArray\<BlueprintRunExport\> | Blueprint entries to export | Non-empty after validation |

### BlueprintRunExport

A single blueprint's export data within a batch.

| Field | Type | Description | Constraints |
|-------|------|-------------|-------------|
| TypeId | int | Blueprint type ID (matches planner's `data-typeid`) | > 0 |
| Name | string | Produced item name (display purposes) | Non-empty |
| Runs | int | Number of manufacturing runs | >= 1 (clamped from 0) |

## Relationships

```text
ProductionBatchExport 1──* BlueprintRunExport
                              │
                              │ matches by TypeId
                              ▼
                    Planner's .bp-check[data-typeid]
```

## State Transitions

```text
                  ┌─────────────┐
                  │   (empty)   │  No pending batch in localStorage
                  └──────┬──────┘
                         │ User clicks "Export to Planner"
                         ▼
                  ┌─────────────┐
                  │   Pending   │  JSON stored under 'pendingProductionBatch'
                  └──────┬──────┘
                         │ Planner loads and reads key
                         ▼
                  ┌─────────────┐
                  │  Consumed   │  Key removed from localStorage
                  └─────────────┘
```

**Overwrite rule**: A new export overwrites any existing `pendingProductionBatch` key (last-write-wins).

## JSON Schema (localStorage contract)

```json
{
  "items": [
    {
      "typeId": 691,
      "name": "Rifter",
      "runs": 5
    },
    {
      "typeId": 11379,
      "name": "Raven",
      "runs": 2
    }
  ]
}
```

## Validation Rules

| Rule | Applied At | Behavior |
|------|-----------|----------|
| Runs < 1 or NaN | Export (optimizer JS) | Clamp to 1 |
| TypeId not found in planner | Import (planner JS) | Skip silently |
| Malformed/corrupt JSON | Import (planner JS) | Ignore key, no error shown |
| Empty items array | Export (optimizer JS) | Button disabled, export blocked |
| localStorage unavailable | Export (optimizer JS) | Show error notification |

## Integration with Existing Models

The `BlueprintRunExport.TypeId` maps to:
- **Optimizer**: `BlueprintRankingEntry.Blueprint.TypeId` (source of export)
- **Planner**: `.bp-check[data-typeid]` DOM attribute and `BlueprintSelection.BlueprintTypeId` (target of import)

The `BlueprintRunExport.Runs` maps to:
- **Optimizer**: The calculated runs value displayed in the "Runs" column of the rankings table
- **Planner**: `.bp-runs[data-typeid]` input value
