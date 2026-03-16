# Contract: localStorage Cross-Page Export

**Key**: `pendingProductionBatch`
**Direction**: Portfolio Optimizer (writer) → Production Planner (reader)
**Lifecycle**: Write-once, read-once, delete-on-consume

## JSON Schema

```json
{
  "type": "object",
  "required": ["items"],
  "properties": {
    "items": {
      "type": "array",
      "minItems": 1,
      "items": {
        "type": "object",
        "required": ["typeId", "name", "runs"],
        "properties": {
          "typeId": {
            "type": "integer",
            "minimum": 1,
            "description": "Blueprint type ID (EVE Online SDE type_id)"
          },
          "name": {
            "type": "string",
            "minLength": 1,
            "description": "Produced item name for display"
          },
          "runs": {
            "type": "integer",
            "minimum": 1,
            "description": "Number of manufacturing runs (clamped from 0)"
          }
        }
      }
    }
  }
}
```

## Example Payload

```json
{
  "items": [
    { "typeId": 691, "name": "Rifter", "runs": 5 },
    { "typeId": 11379, "name": "Raven", "runs": 2 },
    { "typeId": 624, "name": "Badger", "runs": 10 }
  ]
}
```

## Writer Contract (Portfolio Optimizer)

1. Build payload from checked rows using current table state
2. Clamp any runs < 1 to 1
3. Serialize to JSON via `JSON.stringify()`
4. Store via `localStorage.setItem('pendingProductionBatch', json)`
5. Open `/productionplanner` in new tab via `window.open()`

## Reader Contract (Production Planner)

1. After blueprint list is fetched and rendered, check `localStorage.getItem('pendingProductionBatch')`
2. If null or empty, do nothing (normal page behavior)
3. Parse JSON via `JSON.parse()` — on error, ignore silently
4. For each item in `items`:
   - Find `.bp-check[data-typeid="<typeId>"]` — if not found, skip
   - Set checkbox to checked
   - Set `.bp-runs[data-typeid="<typeId>"]` value to item's runs
5. Remove key via `localStorage.removeItem('pendingProductionBatch')`
6. Show notification: "Imported X blueprints from Optimizer"

## Error Handling

| Scenario | Writer Behavior | Reader Behavior |
|----------|----------------|-----------------|
| localStorage unavailable | Show error notification, do not navigate | N/A (graceful degradation) |
| Malformed JSON | N/A (writer always produces valid JSON) | Ignore key, no error shown |
| TypeId not in planner list | N/A | Skip entry silently |
| Empty items array | Export button disabled | No import triggered |
