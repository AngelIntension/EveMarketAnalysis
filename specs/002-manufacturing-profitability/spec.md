# Feature Specification: Manufacturing Profitability Calculator

**Feature Branch**: `002-manufacturing-profitability`
**Created**: 2026-03-13
**Status**: Draft
**Input**: User description: "Manufacturing Profitability Calculator (v1) — Personal Blueprints + Market Data"

## Clarifications

### Session 2026-03-13

- Q: Which market regions should be available for selection? → A: Major trade hub regions only: The Forge (Jita), Domain (Amarr), Sinq Laison (Dodixie), Metropolis (Hek), Heimatar (Rens).
- Q: Should the tax rate be user-adjustable in v1? → A: Yes, user-adjustable with 8% default (simple input field).
- Q: How should the system source blueprint activity data (manufacturing inputs/outputs)? → A: Bundle a static dataset derived from the SDE, updated with game patches.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - View Ranked Profitability Table (Priority: P1)

As a logged-in player with personal blueprints, I want to see a ranked table of items I can manufacture sorted by profitability, so I can decide what to produce next for maximum ISK/hour.

**Why this priority**: This is the core value proposition — without the ranked table, the feature has no purpose. A player needs to see at a glance which of their blueprints are most profitable to manufacture.

**Independent Test**: Can be fully tested by logging in with a character that owns blueprints and verifying a sorted table of manufacturing profitability data appears, showing item names, profit margins, ISK/hour, and material costs.

**Acceptance Scenarios**:

1. **Given** a logged-in player with 10+ personal blueprints, **When** they navigate to the Manufacturing Profitability page, **Then** they see a table of items ranked by ISK/hour (descending) showing: item name, profit margin %, ISK/hour, average daily trade volume, and a materials summary.
2. **Given** a logged-in player viewing the profitability table, **When** they click a column header (profit %, ISK/hour, daily volume), **Then** the table re-sorts by that column.
3. **Given** a logged-in player with more than 50 blueprints, **When** the profitability table loads, **Then** only the top 50 results are displayed to keep the page manageable.
4. **Given** the profitability page is loading data, **When** the player first navigates to the page, **Then** they see a loading state (skeleton placeholders or spinner) while calculations are in progress.

---

### User Story 2 - Profitability Calculation per Blueprint (Priority: P1)

As a player, I want each blueprint's profitability calculated using my actual material efficiency (ME) and time efficiency (TE) levels, current market prices in the selected region, and estimated taxes/fees, so the numbers reflect what I would actually earn.

**Why this priority**: Accurate calculations are essential — without correct ME/TE adjustments, market pricing, and tax estimates, the ranked table would be misleading and potentially cause players to lose ISK.

**Independent Test**: Can be tested by verifying that for a known blueprint (e.g., a Rifter BPC with ME 10, TE 20), the calculated material cost, production time, sell value, and profit match hand-calculated expected values using current market data.

**Acceptance Scenarios**:

1. **Given** a blueprint with ME 10 and TE 20, **When** profitability is calculated, **Then** material quantities are reduced by the ME bonus (standard EVE formula rounding) and production time reflects the TE bonus.
2. **Given** current market data in the selected region, **When** material costs are calculated, **Then** each material uses the lowest available sell order price multiplied by required quantity.
3. **Given** current market data in the selected region, **When** the product sell value is determined, **Then** it uses the highest buy order price, falling back to the lowest sell order price if no buy orders exist.
4. **Given** a calculated profitability, **When** displayed to the user, **Then** the profit accounts for an estimated job installation fee and broker/transaction taxes (default 8% combined rate).

---

### User Story 3 - Authentication Gate (Priority: P1)

As an unauthenticated visitor, I want to be redirected to the login page when I try to access the Manufacturing Profitability page, so that only authenticated players with valid ESI tokens can view their personal data.

**Why this priority**: The feature depends entirely on character-specific data (blueprints, skills). Without authentication, no data can be fetched.

**Independent Test**: Can be tested by navigating to the profitability page without being logged in and confirming a redirect to the login page occurs.

**Acceptance Scenarios**:

1. **Given** an unauthenticated user, **When** they navigate to the Manufacturing Profitability page, **Then** they are redirected to the login page.
2. **Given** a logged-in user with an expired or invalid token, **When** the page attempts to fetch data, **Then** the user is prompted to re-authenticate.

---

### User Story 4 - Region Selection (Priority: P2)

As a player, I want to select which trade hub region to use for market pricing, so I can evaluate profitability based on where I plan to sell my manufactured goods.

**Why this priority**: While The Forge (Jita) is the default and most common market, players who operate in other trade hubs (Amarr, Dodixie, Hek, Rens) need pricing from their local market to make accurate decisions.

**Independent Test**: Can be tested by selecting a different region from the selector and verifying that all market prices, profit calculations, and trade volumes update to reflect the chosen region.

**Acceptance Scenarios**:

1. **Given** a logged-in player on the profitability page, **When** the page loads, **Then** a region selector is visible with The Forge (Jita) selected by default.
2. **Given** a player viewing results for The Forge, **When** they select Domain (Amarr) from the region selector, **Then** all profitability calculations refresh using Domain market data.
3. **Given** a player selects a region where a material has no sell orders, **When** profitability is calculated, **Then** that item shows "Materials unavailable in [region]" instead of misleading values.
4. **Given** a player changes regions, **When** results refresh, **Then** the selected region is visually indicated and persists during the session.

---

### User Story 5 - Graceful Error Handling (Priority: P2)

As a player, I want clear feedback when something goes wrong (no blueprints, ESI errors, missing market data), so I understand the situation and know what to do.

**Why this priority**: Error states are inevitable (new characters with no blueprints, ESI downtime, obscure items with no market data). Without graceful handling, the feature feels broken.

**Independent Test**: Can be tested by simulating each error condition and verifying appropriate user-facing messages appear.

**Acceptance Scenarios**:

1. **Given** a logged-in player with zero blueprints, **When** the profitability page loads, **Then** a friendly message is displayed explaining they have no blueprints and suggesting how to acquire them.
2. **Given** the ESI service is unavailable or returns errors, **When** the page attempts to load, **Then** an error message is shown explaining the data source is temporarily unavailable.
3. **Given** a blueprint whose produced item has no market orders in the selected region, **When** profitability is calculated, **Then** that item is shown with "No market data" instead of misleading zero values.
4. **Given** a partial failure (some blueprints succeed, some fail), **When** results are displayed, **Then** successful calculations are shown and failed items are listed separately with error indicators.

---

### Edge Cases

- What happens when a blueprint is a copy (BPC) vs. an original (BPO)? Both should be included in calculations, with run limits noted for BPCs.
- What happens when a blueprint produces an item that requires materials not traded in the selected region? The item should show with a "materials unavailable in [region]" indicator.
- What happens when market orders exist but with extremely low volume (< 5 orders)? The item should still be calculated but the low volume should be visible to the user via the daily volume column.
- What happens when multiple blueprints produce the same item (e.g., different ME levels)? Each blueprint should be listed as a separate row since profitability differs per ME/TE.
- What happens when a blueprint's manufacturing activity data cannot be resolved (unknown type)? The item should be excluded from results with a logged warning.
- What happens when the player has hundreds of blueprints? The system should still complete within a reasonable time by fetching market data in parallel and caching results, showing the top 50 by default.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST require authentication to access the Manufacturing Profitability page, redirecting unauthenticated users to the login flow.
- **FR-002**: System MUST fetch the authenticated character's personal blueprints from ESI, including type_id, material efficiency, time efficiency, and quantity/runs information.
- **FR-003**: System MUST resolve each blueprint's manufacturing output (produced item type_id) and required materials from a bundled static dataset derived from the EVE Online SDE.
- **FR-004**: System MUST calculate adjusted material quantities for each blueprint using the EVE Online ME formula per single run: `max(1, ceil(base_quantity * (1 - ME/100)))` per material.
- **FR-005**: System MUST calculate production time using: `base_time * (1 - TimeEfficiency/100) * skill_modifiers` where TimeEfficiency is the blueprint's TE level (0-20), and skill modifiers are assumed as 1.0 for v1 (see Assumptions).
- **FR-006**: System MUST fetch current market orders from the selected region for all relevant item types (materials and produced items).
- **FR-007**: System MUST determine material buy cost using the lowest sell order price for each material type in the selected region.
- **FR-008**: System MUST determine product sell value using the highest buy order price in the selected region, falling back to the lowest sell order price if no buy orders exist.
- **FR-009**: System MUST compute gross profit as: sell value - total material cost - estimated taxes/fees.
- **FR-010**: System MUST apply a user-adjustable combined broker/transaction tax rate (default 8%) to the sell value when computing profit.
- **FR-011**: System MUST apply a flat 1% estimated job installation fee based on the estimated item value (sell price) when computing profit.
- **FR-012**: System MUST compute profit margin as: (gross profit / total material cost) * 100.
- **FR-013**: System MUST compute ISK/hour as: gross profit / (production time in hours).
- **FR-014**: System MUST fetch market history from the selected region for each produced item to determine average daily trade volume.
- **FR-015**: System MUST display results in a table with columns: item name, profit margin %, ISK/hour, average daily volume, and required materials summary.
- **FR-016**: System MUST sort the table by ISK/hour descending by default.
- **FR-017**: System MUST allow the user to re-sort the table by clicking column headers. Sortable columns: profit margin %, ISK/hour, and daily volume.
- **FR-018**: System MUST limit displayed results to the top 50 items (by default sort) to maintain usability.
- **FR-019**: System MUST show a loading state while profitability data is being calculated.
- **FR-020**: System MUST display appropriate messages for error conditions: no blueprints, ESI unavailable, missing market data.
- **FR-021**: System MUST cache market data (orders and history) for 5 minutes to reduce ESI load and improve response time.
- **FR-022**: System MUST handle partial failures gracefully — showing results for successful calculations and indicating errors for failed ones.
- **FR-023**: System MUST provide a region selector with five trade hub regions: The Forge (Jita), Domain (Amarr), Sinq Laison (Dodixie), Metropolis (Hek), Heimatar (Rens).
- **FR-024**: System MUST default the region selector to The Forge (Jita).
- **FR-025**: System MUST recalculate all profitability data when the user changes the selected region.
- **FR-026**: System MUST persist the selected region for the duration of the user's browser session (client-side state; lost on page refresh is acceptable for v1).
- **FR-027**: System MUST provide a tax rate input field defaulting to 8%, allowing users to adjust the combined broker/transaction tax percentage.
- **FR-028**: System MUST recalculate all profitability data when the user changes the tax rate.
- **FR-029**: System MUST persist the user-adjusted tax rate for the duration of the user's browser session (client-side state; lost on page refresh is acceptable for v1).

### Key Entities

- **Blueprint**: A player-owned manufacturing blueprint with type identifier, material efficiency level (0-10), time efficiency level (0-20), quantity (for BPOs) or runs remaining (for BPCs), and whether it is an original or copy.
- **Manufacturing Output**: The relationship between a blueprint and the item it produces, including base material requirements (type and quantity) and base production time.
- **Market Snapshot**: A point-in-time capture of market conditions for a specific item type in a specific region, including lowest sell price, highest buy price, and average daily trade volume.
- **Profitability Calculation**: The computed result for a single blueprint, combining blueprint properties, adjusted materials, market prices, taxes/fees, and derived metrics (gross profit, margin %, ISK/hour).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can identify their most profitable manufacturing option within 30 seconds of navigating to the page (including load time).
- **SC-002**: Profitability calculations are accurate to within 5% of a manual calculation using the same market data snapshot (accounting for ME/TE adjustments, taxes, and fees).
- **SC-003**: The profitability table loads and displays results for a character with up to 100 blueprints without the user perceiving an unresponsive page (loading state appears immediately, results populate progressively or within a reasonable wait).
- **SC-004**: Users can compare profitability across all their blueprints in a single view without needing to navigate between multiple pages or perform manual calculations.
- **SC-005**: Market data shown is no more than 10 minutes stale, ensuring profitability estimates reflect recent market conditions.
- **SC-006**: 100% of error conditions (no blueprints, ESI errors, missing market data) display user-friendly messages rather than raw errors or blank screens.

## Assumptions

- **Skill modifiers for production time**: v1 assumes a skill modifier of 1.0 (no skill-based time reduction). Incorporating character skills into time calculations is deferred to a future iteration (listed as P2 nice-to-have).
- **Job installation fee**: Estimated as a flat 1% of the produced item's estimated value. Actual EVE Online system cost indices vary by solar system and are complex to model; this is a reasonable approximation for v1.
- **Tax rate**: Default 8% combined broker fee + transaction tax, user-adjustable via an input field. This allows players with higher trade skills (who pay lower taxes) to get accurate profitability numbers.
- **Region**: Market data is fetched from the user-selected region. Five major trade hub regions are supported: The Forge (Jita, default), Domain (Amarr), Sinq Laison (Dodixie), Metropolis (Hek), Heimatar (Rens).
- **Blueprint activity data**: A bundled static dataset derived from the EVE Online SDE provides the mapping of blueprint type_ids to their manufacturing inputs (materials) and outputs (produced items). This dataset is updated when game patches change blueprint data (typically every few months).
- **Pagination**: Top 50 results displayed. No explicit pagination controls in v1; if players want to see more, this can be added in a future iteration.
- **BPO vs BPC**: Both are included. BPCs are calculated based on a single run. BPOs are calculated based on a single run as well (since they have unlimited runs).
