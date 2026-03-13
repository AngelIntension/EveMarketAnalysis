# Page Handler Contracts: Manufacturing Profitability

**Branch**: `002-manufacturing-profitability` | **Date**: 2026-03-13

## Overview

The Manufacturing Profitability page follows the existing skeleton loading pattern established by `CharacterSummary`. The page renders a shell with skeleton placeholders, then JavaScript calls named handlers to fetch data asynchronously.

## Page: `/ManufacturingProfitability`

### GET `/ManufacturingProfitability` (OnGet)

**Purpose**: Render the page shell with skeleton loading placeholders.

**Authorization**: Required (redirect to `/Auth/Login` if unauthenticated)

**Response**: Razor Page HTML with:
- Region selector dropdown (5 trade hubs, default: The Forge)
- Tax rate input field (default: 8%)
- Empty profitability table with skeleton placeholders
- JavaScript to call `OnGetProfitabilityAsync` on page load

### GET `/ManufacturingProfitability?handler=Profitability` (OnGetProfitabilityAsync)

**Purpose**: Compute and return profitability data for the authenticated character's blueprints.

**Authorization**: Required (returns 401 if unauthenticated)

**Query Parameters**:

| Parameter | Type | Required | Default | Description |
|-----------|------|----------|---------|-------------|
| regionId | int | No | 10000002 | Trade hub region ID |
| taxRate | decimal | No | 0.08 | Combined broker/transaction tax rate (0.0 - 1.0) |

**Validation**:
- `regionId` must be one of: 10000002, 10000043, 10000032, 10000042, 10000030
- `taxRate` must be between 0.0 and 1.0 (inclusive)
- Invalid values return 400 Bad Request

**Response (200 OK)**: `JsonResult` containing:

```json
{
  "results": [
    {
      "blueprintItemId": 1234567890,
      "producedTypeName": "Rifter",
      "producedTypeId": 587,
      "materialEfficiency": 10,
      "timeEfficiency": 20,
      "isCopy": false,
      "totalMaterialCost": 1250000.50,
      "productSellValue": 1800000.00,
      "taxAmount": 144000.00,
      "installationFee": 18000.00,
      "grossProfit": 387999.50,
      "profitMarginPercent": 31.04,
      "productionTimeSeconds": 2880,
      "iskPerHour": 485000.00,
      "averageDailyVolume": 1250.5,
      "materials": [
        {
          "typeName": "Tritanium",
          "typeId": 34,
          "adjustedQuantity": 23040,
          "unitCost": 5.25,
          "totalCost": 120960.00
        }
      ],
      "hasMarketData": true,
      "errorMessage": null
    }
  ],
  "regionId": 10000002,
  "regionName": "The Forge",
  "taxRate": 0.08,
  "totalBlueprints": 45,
  "successCount": 42,
  "errorCount": 3,
  "fetchedAt": "2026-03-13T14:30:00Z"
}
```

**Response (400 Bad Request)**: Invalid regionId or taxRate
**Response (401 Unauthorized)**: Not authenticated
**Response (500 Internal Server Error)**: Unrecoverable ESI/service failure

**Sort order**: Results sorted by `iskPerHour` descending, limited to top 50.

## Service Interfaces

### IEsiMarketClient

```
GetMarketOrdersAsync(regionId, typeId) → MarketSnapshot
GetMarketHistoryAsync(regionId, typeId) → MarketHistory
```

### IBlueprintDataService

```
GetBlueprintActivity(blueprintTypeId) → BlueprintActivity?
GetAllBlueprintActivities() → IReadOnlyDictionary<int, BlueprintActivity>
```

### IProfitabilityCalculator

```
CalculateAsync(blueprints, settings) → ImmutableArray<ProfitabilityResult>
```

### IEsiCharacterClient (extended)

```
GetCharacterBlueprintsAsync(characterId) → ImmutableArray<CharacterBlueprint>
```
