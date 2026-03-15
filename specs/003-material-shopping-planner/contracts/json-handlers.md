# JSON Handler Contracts: Production Planner

**Branch**: `003-material-shopping-planner` | **Date**: 2026-03-15

These contracts define the JSON endpoints exposed by the `ProductionPlanner` Razor Page via named handlers, following the established pattern from `ManufacturingProfitability`.

---

## Handler 1: Generate Shopping List

**URL**: `GET /ProductionPlanner?handler=ShoppingList`

**Query Parameters**:

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| selections | string (JSON) | Yes | — | JSON-encoded array of blueprint selections |

**Request `selections` JSON shape**:

```json
[
  {
    "blueprintTypeId": 681,
    "runs": 5,
    "produceComponents": true
  }
]
```

**Response** (200 OK):

```json
{
  "items": [
    {
      "typeId": 34,
      "typeName": "Tritanium",
      "category": "Mineral",
      "totalQuantity": 125000,
      "volume": 0.01,
      "totalVolume": 1250.0,
      "estimatedUnitCost": null,
      "estimatedTotalCost": null,
      "sources": [
        {
          "blueprintName": "Rifter Blueprint",
          "blueprintTypeId": 681,
          "quantity": 125000
        }
      ]
    }
  ],
  "totalEstimatedCost": null,
  "totalVolume": 1250.0,
  "blueprintCount": 1,
  "generatedAt": "2026-03-15T12:00:00Z",
  "errors": []
}
```

**Error Response** (200 with error field):

```json
{
  "error": "No blueprints selected"
}
```

**Notes**:
- Costs are `null` until `OnGetCostsAsync` is called separately
- `errors` array contains per-blueprint warnings (e.g., "Blueprint X not found in SDE")
- Empty `selections` returns error response

---

## Handler 2: Fetch Costs

**URL**: `GET /ProductionPlanner?handler=Costs`

**Query Parameters**:

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| regionId | int | No | 10000002 | Trade hub region ID |
| typeIds | string | Yes | — | Comma-separated list of type IDs to price |

**Response** (200 OK):

```json
{
  "regionId": 10000002,
  "regionName": "The Forge",
  "costs": [
    {
      "typeId": 34,
      "unitCost": 4.50,
      "volume": 0.01,
      "available": true
    },
    {
      "typeId": 35,
      "unitCost": null,
      "volume": 0.10,
      "available": false
    }
  ],
  "fetchedAt": "2026-03-15T12:00:05Z"
}
```

**Validation**:
- `regionId` must match a known `TradeHubRegion`; invalid values return error
- `typeIds` must contain at least one valid integer

---

## Handler 3: Get Blueprints

**URL**: `GET /ProductionPlanner?handler=Blueprints`

**Query Parameters**: None (uses authenticated character from session)

**Response** (200 OK):

```json
{
  "blueprints": [
    {
      "typeId": 681,
      "typeName": "Rifter Blueprint",
      "materialEfficiency": 10,
      "timeEfficiency": 20,
      "runs": -1,
      "isCopy": false,
      "producedTypeId": 587,
      "producedTypeName": "Rifter"
    }
  ]
}
```

**Notes**:
- Enriches `CharacterBlueprint` with `producedTypeId` and `producedTypeName` from `BlueprintDataService`
- Blueprints without matching SDE data are excluded (with warning logged)
- Results cached for the duration of the page session
