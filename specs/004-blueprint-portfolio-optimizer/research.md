# Research: Blueprint Portfolio Optimizer

**Branch**: `004-blueprint-portfolio-optimizer` | **Date**: 2026-03-15

## R1: Pricing Model — Buy Orders vs Sell Orders

**Decision**: Material costs use highest buy order at procurement station; product revenue uses lowest sell order at selling hub. This reverses the existing ProfitabilityCalculator pricing convention.

**Rationale**: The user explicitly specified this model. It reflects a patient industrialist strategy: procure materials via buy orders (cheaper, slower) and sell products by undercutting the lowest sell order (competitive, faster). The existing `MarketSnapshot` already provides both `HighestBuyPrice` and `LowestSellPrice` per station, so no new ESI calls are needed — only the field selection changes.

**Alternatives considered**:
- Use lowest sell for materials (instant buy) + highest buy for products (instant sell) — this is the current ProfitabilityCalculator approach. Rejected because user wants the opposite pricing model for this feature.
- Use volume-weighted average — rejected because user explicitly specified order-book extremes.

**Impact**: The new `PortfolioAnalyzer` service will use `MarketSnapshot.HighestBuyPrice` for material costs and `MarketSnapshot.LowestSellPrice` for product revenue. The existing `ProfitabilityCalculator` and `EsiMarketClient` remain unchanged.

## R2: Phase Data Source — Static JSON

**Decision**: Phase definitions stored as embedded JSON resource (`phases.json`) mapping phase ordinal to name + array of candidate type IDs per category.

**Rationale**: The spec mandates "static embedded data that ships with the application." This matches the existing `blueprints.json` pattern used by `BlueprintDataService`. Loading is lazy, cached indefinitely (singleton lifetime). Type IDs are curated manually per phase.

**Alternatives considered**:
- Database-driven phases — rejected; overkill for 5 static phases, adds persistence complexity.
- Config file (appsettings.json) — rejected; phase data is application data, not configuration.
- SDE (Static Data Export) lookup — rejected; too complex for v1, would need category/group ID mapping logic.

**Impact**: New `PhaseService` singleton loads `phases.json` at first access. Format mirrors `BlueprintDataService` lazy-loading pattern.

## R3: System Cost Index — ESI Industry Systems Endpoint

**Decision**: Fetch manufacturing cost index from ESI `GET /industry/systems/` endpoint, cached for 1 hour (data updates infrequently). Filter to the user's configured manufacturing system.

**Rationale**: The ESI `/industry/systems/` endpoint returns cost indices for all systems in a single call (~6000 entries). This is a bulk endpoint (constitution-preferred). The manufacturing activity ID is `1`. Cache for 1 hour since cost indices update once per day in EVE.

**Alternatives considered**:
- Hardcoded cost index — rejected; varies significantly by system.
- Per-system API call — no such endpoint exists; the bulk endpoint is the only option.

**Impact**: The `PortfolioAnalyzer` will call ESI for industry systems data, filter to the configured system's solar system ID, and extract the manufacturing cost index. This index multiplies the estimated item value to produce the system cost fee.

## R4: Skill Gating — Blueprint Prerequisite Skills

**Decision**: Reuse existing `EsiCharacterClient.GetCharacterSkillsAsync()` to fetch character skills. Cross-reference against blueprint manufacturing prerequisites from the SDE/blueprints data. Blueprints whose prerequisites are not met are excluded from the ranking.

**Rationale**: The spec requires skill gating (FR-005). The character's trained skills are already fetchable. Blueprint manufacturing prerequisites (required skills + levels) need to come from static data. The existing `blueprints.json` does not include skill prerequisites, so this data will need to be added to the phase definitions or a separate skills mapping.

**Alternatives considered**:
- Ignore skill gating in v1 — rejected; it's a P1 requirement (FR-005).
- Fetch prerequisites dynamically from ESI per type — rejected; too many API calls. Better to embed in static data.

**Impact**: Extend `phases.json` or add a separate `skill-requirements.json` that maps blueprint type IDs to required skill IDs + levels. `PortfolioAnalyzer` cross-references character skills against these requirements.

## R5: NPC Seeded BPO Prices

**Decision**: Use the SDE `blueprints.json` to include NPC base prices for BPOs, or fetch from ESI `GET /markets/prices/` (which returns NPC-adjusted prices for all types).

**Rationale**: FR-010 requires showing NPC seeded prices alongside player market prices for BPO purchase recommendations. The ESI `GET /markets/prices/` endpoint returns `adjusted_price` and `average_price` for all types — the `adjusted_price` closely corresponds to NPC seeded prices for seeded items.

**Alternatives considered**:
- Embed NPC prices in phases.json — rejected; prices may change with patches and would need manual updates.
- Use only player market prices — rejected; spec explicitly requires both.

**Impact**: `PortfolioAnalyzer` fetches `GET /markets/prices/` (cacheable for 1 hour), uses `adjusted_price` as the NPC seeded price proxy. Player market price comes from existing `EsiMarketClient.GetMarketSnapshotAsync()` with region-wide orders (not station-filtered) for BPO recommendations.

## R6: Refresh Analysis UX Pattern

**Decision**: Follow the existing ManufacturingProfitability page pattern: skeleton loading on initial page load, AJAX fetch on "Refresh Analysis" button click, loading spinner during computation.

**Rationale**: This matches the established codebase pattern (skeleton → AJAX → render). The "Refresh Analysis" button replaces the existing "Refresh" button but adds the constraint that all control changes are batched until the button is clicked. Local storage reads/writes happen in JavaScript on the client side.

**Alternatives considered**:
- Blazor Server with SignalR — rejected; would introduce a new technology stack. The app is Razor Pages throughout.
- Full page reload — rejected; poor UX for parameter changes.

**Impact**: Page model has named handlers (e.g., `OnGetAnalysisAsync`) that accept all configuration as query parameters. JavaScript reads local storage, populates controls, and sends all values on Refresh click.

## R7: Region-Wide BPO Market Data

**Decision**: For BPO purchase recommendations, fetch market orders region-wide (not station-filtered) to show the best available player price across the entire region.

**Rationale**: The user clarified that BPO prices should be region-wide since BPOs are infrequent purchases where travel is trivial. The existing `EsiMarketClient.GetMarketSnapshotAsync()` already fetches region orders but filters to trade hub stations. For BPO recommendations, we need a variant that does not apply the station filter.

**Alternatives considered**:
- Reuse station-filtered data — rejected; user explicitly wants region-wide for BPOs.
- New separate service — rejected; simpler to add an optional parameter or a second method to EsiMarketClient.

**Impact**: Add `GetRegionMarketSnapshotAsync(int regionId, int typeId)` to `IEsiMarketClient` that returns lowest sell across the entire region (no station filter). Used only for BPO purchase recommendations.
