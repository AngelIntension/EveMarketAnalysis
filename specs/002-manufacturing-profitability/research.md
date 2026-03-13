# Research: Manufacturing Profitability Calculator

**Branch**: `002-manufacturing-profitability` | **Date**: 2026-03-13

## R1: SDE Blueprint Activity Data — Format & Bundling Strategy

**Decision**: Bundle a JSON file containing only the manufacturing activity data extracted from the EVE SDE `blueprints.yaml`.

**Rationale**: The full SDE `blueprints.yaml` is ~70MB and contains activities beyond manufacturing (copying, invention, reactions, research). Extracting only the manufacturing subset into a compact JSON structure reduces the bundled file to ~2-5MB and allows fast in-memory lookup. JSON is natively deserializable in .NET without additional dependencies.

**Alternatives considered**:
- **Full SDE YAML bundle**: Too large (~70MB), requires a YAML parser dependency, includes unused data.
- **SQLite SDE dump**: Adds a database dependency for what is essentially a read-only lookup table. Overkill for v1.
- **Third-party API (Fuzzwork/EVERef)**: Adds runtime external dependency and failure mode. The data rarely changes (only on game patches), making a static bundle more reliable.

**Data structure for bundled JSON**:
```json
{
  "blueprintTypeId": {
    "producedTypeId": 12345,
    "producedQuantity": 1,
    "time": 3600,
    "materials": [
      { "typeId": 34, "quantity": 100 },
      { "typeId": 35, "quantity": 50 }
    ]
  }
}
```

**Source**: https://developers.eveonline.com/docs/resources/sde/ — download the latest SDE export, parse `blueprints.yaml`, extract `activities.manufacturing` for each blueprint.

## R2: ESI Market Orders — Pagination & Filtering

**Decision**: Use `GET /markets/{region_id}/orders/?type_id={type_id}&order_type=sell` (and `buy`) with type_id filtering to avoid full region scans. Paginate via `X-Pages` response header when results exceed one page.

**Rationale**: Fetching all orders for a region without type_id filter returns millions of records across hundreds of pages. Filtering by type_id returns typically 1-3 pages per item, which is manageable. The Kiota-generated client already supports these query parameters.

**Alternatives considered**:
- **Full region order dump + client-side filter**: Extremely slow (hundreds of pages), wastes bandwidth, hits rate limits.
- **`/markets/prices/` adjusted prices**: These are CCP-calculated averages, not current live prices. Not suitable for profitability calculations.

**Pagination pattern**:
1. First request returns `X-Pages` header with total page count.
2. If `X-Pages > 1`, dispatch pages 2..N in parallel via `Task.WhenAll`.
3. Aggregate all order arrays into a single collection.

## R3: EVE Online ME Formula — Exact Calculation

**Decision**: Use the standard EVE ME formula: `max(1, ceil(base_quantity * (1 - ME/100)))` per material per single run.

**Rationale**: This is the canonical formula used by EVE Online for material efficiency reductions. The `max(1, ...)` ensures at least 1 unit of each material is always required. The `ceil()` rounds up fractional quantities since partial materials cannot be consumed.

**Note**: For `runs > 1`, the formula is applied per-run and then multiplied: `max(runs, ceil(runs * base_quantity * (1 - ME/100)))`. Since v1 calculates profitability per single run, we use the single-run variant.

**Alternatives considered**:
- **Simplified percentage reduction**: Loses accuracy due to rounding behavior. EVE players expect exact numbers.

## R4: Market History for Daily Volume

**Decision**: Use `GET /markets/{region_id}/history/?type_id={type_id}` and compute average daily volume from the last 30 days of entries.

**Rationale**: The history endpoint returns daily aggregates (volume, average price, highest, lowest, order_count) per type per region. Averaging the last 30 days gives a stable daily volume indicator that smooths out spikes.

**Alternatives considered**:
- **Single day volume**: Too volatile — a single bad day could make a profitable item look dead.
- **Median instead of average**: More robust against outliers, but EVE players are accustomed to average daily volume (ADV) as the standard metric.

## R5: Region ID Mapping for Trade Hubs

**Decision**: Hard-code the five major trade hub region IDs as a static lookup.

| Region | Region ID | Trade Hub |
|--------|-----------|-----------|
| The Forge | 10000002 | Jita |
| Domain | 10000043 | Amarr |
| Sinq Laison | 10000032 | Dodixie |
| Metropolis | 10000042 | Hek |
| Heimatar | 10000030 | Rens |

**Rationale**: These IDs are permanent and never change. A static mapping avoids unnecessary API calls. Five entries do not warrant a database or external lookup.

## R6: Caching Strategy for Market Data

**Decision**: Cache market orders and history per (region_id, type_id) key with 5-minute sliding expiration, aligned with ESI cache headers.

**Rationale**: ESI market order endpoints have a 5-minute cache header (`Cache-Control: max-age=300`). Respecting this avoids redundant requests and rate limit consumption. Per-type-per-region caching ensures that changing regions fetches fresh data for the new region without invalidating the old region's cache (useful if user switches back).

**Cache key format**: `market:orders:{regionId}:{typeId}` and `market:history:{regionId}:{typeId}`

**Alternatives considered**:
- **Per-region bulk cache**: Would require fetching all types for a region at once, which is too expensive.
- **10-minute cache**: Feasible but ESI only guarantees 5-minute freshness; 10 minutes risks stale data.

## R7: Parallel Market Data Fetching Strategy

**Decision**: Collect all unique type_ids (materials + products) across all blueprints, then dispatch market order fetches in parallel with a concurrency limiter (max 20 concurrent requests) to stay within ESI rate limits.

**Rationale**: A character with 50 blueprints might need market data for ~150-200 unique type_ids. Fetching sequentially would take minutes. Parallel dispatch with rate limiting completes in seconds while respecting ESI's error limit. The existing `EsiRateLimitHandler` already provides back-off when approaching limits.

**Alternatives considered**:
- **Unbounded parallelism**: Risks hitting ESI rate limits and getting temporarily blocked.
- **Sequential fetching**: Far too slow for any meaningful number of blueprints.

## R8: Skeleton Loading Pattern for Profitability Page

**Decision**: Follow the existing `CharacterSummary` page pattern — render the page shell immediately with skeleton placeholders, then fetch profitability data via a named handler (`OnGetProfitabilityAsync`) called from JavaScript.

**Rationale**: This matches the established pattern in the codebase, maintains UI consistency, and provides immediate visual feedback. The CharacterSummary page already implements this exact approach successfully.

**Alternatives considered**:
- **Server-side rendering with loading spinner**: Blocks the entire page until data is ready, which could be 10-30 seconds for many blueprints.
- **SignalR/WebSocket streaming**: Over-engineered for v1; adds complexity without proportional benefit.
