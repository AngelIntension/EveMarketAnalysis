# AJAX Endpoint Contracts: Portfolio Optimizer

**Branch**: `004-blueprint-portfolio-optimizer` | **Date**: 2026-03-15

## Endpoints

All endpoints are Razor Page named handlers on `/PortfolioOptimizer`. Authentication required (cookie-based, existing flow).

---

### GET /PortfolioOptimizer?handler=Analysis

**Purpose**: Full portfolio analysis — returns rankings, phase statuses, recommendations.

**Query Parameters**:

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| procurementRegionId | int | 10000002 | Procurement station's region ID |
| procurementStationId | long | 60003760 | Procurement station ID |
| sellingRegionId | int | 10000002 | Selling hub's region ID |
| sellingStationId | long | 60003760 | Selling hub station ID |
| manufacturingSystemId | int | 30000142 | Manufacturing system for cost index |
| buyingBrokerFee | decimal | 3.0 | Buying broker fee % |
| sellingBrokerFee | decimal | 3.0 | Selling broker fee % |
| salesTax | decimal | 3.6 | Sales tax % |
| minIskPerHour | decimal | 25000000 | Minimum ISK/hr threshold |
| dailyIncomeGoal | decimal | 750000000 | Daily income goal |
| manufacturingSlots | int | 11 | Manufacturing slot count |
| whatIfME | int? | null | What-if ME override (0-10) |
| whatIfTE | int? | null | What-if TE override (0-20) |
| phaseOverride | int? | null | Manual phase override (1-5) |
| simulateNextPhase | bool | false | Preview analysis as if in next phase (does not persist) |

**Response**: `200 OK` with `PortfolioAnalysis` JSON:

```json
{
  "rankings": [
    {
      "blueprint": { "itemId": 123, "typeId": 456, "typeName": "...", "materialEfficiency": 10, "timeEfficiency": 20, "runs": -1, "isCopy": false },
      "producedTypeName": "Rifter",
      "producedTypeId": 587,
      "phaseNumber": 1,
      "materialCost": 1500000.0,
      "productRevenue": 2200000.0,
      "buyingBrokerFee": 45000.0,
      "sellingBrokerFee": 66000.0,
      "salesTax": 79200.0,
      "systemCostFee": 22000.0,
      "grossProfit": 487800.0,
      "profitMarginPercent": 32.52,
      "productionTimeSeconds": 3600.0,
      "iskPerHour": 487800.0,
      "averageDailyVolume": 150.5,
      "isCurrentPhase": true,
      "meetsThreshold": true,
      "hasMarketData": true,
      "errorMessage": null
    }
  ],
  "phaseStatuses": [
    {
      "phase": { "phaseNumber": 1, "name": "T1 Frigate Foundation", "description": "...", "candidateTypeIds": [456, 789] },
      "ownedProfitableCount": 9,
      "requiredCount": 9,
      "isComplete": true,
      "dailyPotentialIncome": 120000000.0,
      "completionReason": "slots"
    }
  ],
  "currentPhaseNumber": 2,
  "phaseOverrideActive": false,
  "bpoRecommendations": [
    {
      "blueprintTypeId": 999,
      "blueprintName": "Thrasher Blueprint",
      "producedTypeName": "Thrasher",
      "phaseNumber": 2,
      "npcSeededPrice": 5000000.0,
      "playerMarketPrice": 4500000.0,
      "projectedIskPerHour": 35000000.0,
      "paybackPeriodDays": 3.4,
      "roiPercent": 882.35,
      "hasMarketData": true,
      "errorMessage": null
    }
  ],
  "researchRecommendations": [
    {
      "blueprint": { "itemId": 124, "typeId": 457, "typeName": "...", "materialEfficiency": 5, "timeEfficiency": 10, "runs": -1, "isCopy": false },
      "producedTypeName": "Punisher",
      "currentIskPerHour": 20000000.0,
      "projectedIskPerHour": 28000000.0,
      "iskPerHourGain": 8000000.0,
      "gainPercent": 40.0,
      "currentME": 5,
      "currentTE": 10,
      "targetME": 10,
      "targetTE": 20
    }
  ],
  "totalBlueprintsEvaluated": 45,
  "successCount": 42,
  "errorCount": 3,
  "portfolioSizeWarning": false,
  "fetchedAt": "2026-03-15T14:30:00+00:00"
}
```

**Error Responses**:
- `401 Unauthorized` — Not authenticated
- `400 Bad Request` — Invalid parameter values (validation failures)

---

### GET /PortfolioOptimizer (initial page load)

**Purpose**: Renders the page shell with skeleton loaders. No data fetched server-side.

**Response**: HTML page with:
- Station/hub selectors (populated from `TradeHubRegion.All`)
- ME/TE sliders (0-10, 0-20)
- Threshold inputs (min ISK/hr, daily goal, slots)
- Broker/tax inputs
- "Refresh Analysis" button
- Skeleton loading placeholders for all four sections
- JavaScript that reads local storage and populates controls on load
