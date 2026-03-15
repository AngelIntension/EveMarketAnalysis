# Feature Specification: Blueprint Portfolio Optimizer

**Feature Branch**: `004-blueprint-portfolio-optimizer`
**Created**: 2026-03-15
**Status**: Draft
**Input**: User description: "Blueprint Portfolio Optimizer – Realistic ISK/hr Ranking + Phased Production Roadmap + Research Queue Filler + Recommended BPO Purchases"

## Clarifications

### Session 2026-03-15

- Q: How should material costs and product revenue be priced? → A: Material costs use highest buy order at the procurement station. Product revenue uses lowest sell order at the selling hub. All pricing is station-level.
- Q: How are broker fees and sales tax handled? → A: Three global configurable percentages persisted in local storage: buying broker fee % (procurement station), selling broker fee % (selling hub), and sales tax % (selling hub).
- Q: How should broker/tax settings and station selections be persisted? → A: Browser local storage. Persists across sessions in the same browser without server-side infrastructure.
- Q: Should the current phase be recalculated or persisted? → A: Persist manual overrides in local storage. Auto-calculated phase serves as baseline; manual "Advance Phase" overrides survive across sessions.
- Q: Which system's cost index should be used? → A: User-configurable manufacturing system, persisted in local storage, defaulting to Jita.
- Q: How should BPO purchase prices be sourced? → A: Show both NPC seeded price and player market price side by side. BPO prices use region-wide orders (not station-restricted) since BPOs are infrequent purchases where extra travel for the best price is trivial.
- Q: Which phase should BPO purchase recommendations show? → A: Current phase until phase completion triggers. Once the phase advances (auto or manual), recommendations shift to the next phase's unowned BPOs. The owned ranking table continues to show blueprints from all phases.
- Q: How should controls trigger recalculation? → A: No live/automatic updates. A single "Refresh Analysis" button triggers all recalculation. Controls update their local state immediately but no computation occurs until Refresh is clicked. The button shows loading state while calculating and is disabled during refresh.
- Q: What performance safeguards are needed? → A: Market data cached with 5-minute sliding expiration per station/region + type_id. Concurrent ESI calls limited to 20. Rate-limited requests show user-friendly retry message. Soft cap of ~200–300 blueprints per refresh with warning if exceeded.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Owned Blueprint ISK/hr Ranking (Priority: P1)

An industrialist navigates to `/portfoliooptimizer` and sees a sortable table of all their owned blueprints ranked by realistic ISK/hr. The ISK/hr calculation accounts for system cost index fees, buying broker fee, selling broker fee, sales tax, material costs (highest buy order at the procurement station), and product revenue (lowest sell order at the selling hub). All pricing is station-level. The user selects a single procurement station and a single selling hub. ME/TE what-if sliders allow the user to see how research levels would affect profitability. All changes take effect when the user clicks the "Refresh Analysis" button.

**Why this priority**: This is the core value proposition — knowing which blueprints to manufacture right now. Without accurate ISK/hr ranking, no other feature (phases, recommendations) can function.

**Independent Test**: Can be fully tested by logging in with a character that owns blueprints, navigating to the page, and verifying the table displays with correct ISK/hr values that match manual calculations.

**Acceptance Scenarios**:

1. **Given** an authenticated user with owned blueprints, **When** they navigate to `/portfoliooptimizer`, **Then** they see a table of their blueprints sorted by ISK/hr descending, with current-phase items highlighted.
2. **Given** a blueprint ranking table is displayed, **When** the user changes the selling hub and clicks "Refresh Analysis", **Then** all ISK/hr values recalculate using that hub's sell order prices.
3. **Given** a blueprint ranking table is displayed, **When** the user changes the procurement station and clicks "Refresh Analysis", **Then** all material costs recalculate using that station's buy order prices.
4. **Given** a blueprint ranking table is displayed, **When** the user adjusts ME/TE what-if sliders and clicks "Refresh Analysis", **Then** ISK/hr values update to reflect the hypothetical research levels.
5. **Given** the user has blueprints they lack skills to manufacture, **When** the ranking loads, **Then** those blueprints are excluded (skill-gated).
6. **Given** the user adjusts multiple controls (station, ME/TE, thresholds) without clicking Refresh, **When** they click "Refresh Analysis", **Then** all changes are applied in one batch calculation.
7. **Given** a refresh is in progress, **When** the user views the Refresh Analysis button, **Then** it shows a loading spinner and is disabled until the calculation completes.

---

### User Story 2 - Phased Production Roadmap (Priority: P1)

The user sees a roadmap of five production phases displayed as grouped cards with progress indicators. Each phase shows how many profitable blueprints the user owns versus the threshold needed for phase completion. The current phase is clearly indicated, and completed phases are visually distinguished.

**Why this priority**: The roadmap provides the strategic context for all recommendations. Users need to see where they are in their progression to understand why specific BPOs are recommended.

**Independent Test**: Can be tested by verifying phase cards render with correct counts of owned profitable blueprints per phase, and that the current phase indicator matches the phase-completion logic.

**Acceptance Scenarios**:

1. **Given** an authenticated user, **When** the roadmap loads, **Then** five phase cards are displayed with names: "T1 Frigate Foundation", "T1 Destroyer Expansion", "T1 Cruiser & Battlecruiser Tier", "Battleship Tier", "Capital & Advanced Entry".
2. **Given** a user owns 9+ profitable blueprints (≥ configurable ISK/hr minimum) in Phase 1, **When** the roadmap evaluates phase completion, **Then** Phase 1 is marked complete and the current phase advances to Phase 2.
3. **Given** a user's total daily potential income exceeds the configurable daily goal, **When** the roadmap evaluates phase completion, **Then** the phase advances as a secondary fallback trigger.
4. **Given** the user clicks "Advance Phase" override, **When** confirmed, **Then** the current phase advances regardless of completion criteria.

---

### User Story 3 - Recommended BPO Purchases (Priority: P2)

The user sees a list of unowned BPOs from the current phase until phase completion triggers (auto or manual). Once the phase advances, recommendations shift to show unowned BPOs from the next phase only. Each recommendation shows the BPO's NPC seeded price, player market lowest sell price (region-wide), projected post-research ISK/hr, ROI percentage, and payback period. The owned ranking table continues to show blueprints from all phases regardless of current phase.

**Why this priority**: Purchase recommendations directly drive the user's investment decisions and slot utilization growth. Depends on phase determination (P1) being functional.

**Independent Test**: Can be tested by verifying that a user in Phase 1 sees Phase 1 BPO recommendations, and after advancing to Phase 2, sees Phase 2 recommendations.

**Acceptance Scenarios**:

1. **Given** a user currently in Phase 1 (not yet completed), **When** the recommendations load, **Then** only Phase 1 BPOs the user does not own are shown.
2. **Given** a user has completed Phase 1 and advanced to Phase 2, **When** the recommendations load, **Then** only Phase 2 BPOs the user does not own are shown.
3. **Given** a recommended BPO, **When** displayed, **Then** it shows both NPC seeded price and player market lowest sell price (region-wide), projected ISK/hr at max ME/TE, ROI percentage, and payback period in days.
4. **Given** the user changes the selling hub and clicks "Refresh Analysis", **When** recommendations refresh, **Then** projected ISK/hr updates for each recommended BPO.
5. **Given** the user is in Phase 5 (final phase) and has completed it, **When** recommendations load, **Then** the section indicates no further phases and shows no purchase recommendations.

---

### User Story 4 - Research Queue Recommendations (Priority: P2)

The user sees a prioritized list of 5–10 blueprints they should research or invent next. This considers owned blueprints that are not yet fully researched and prioritizes by projected ISK/hr gain from additional ME/TE research.

**Why this priority**: Research queue optimization ensures the user is always improving their existing blueprints between manufacturing runs. Depends on ISK/hr calculation (P1).

**Independent Test**: Can be tested by verifying that a user with partially-researched blueprints sees correct recommendations ordered by projected ISK/hr improvement.

**Acceptance Scenarios**:

1. **Given** a user owns blueprints with ME/TE below maximum, **When** the research section loads, **Then** 5–10 blueprints are listed ordered by projected ISK/hr gain from research.
2. **Given** all owned blueprints are fully researched, **When** the research section loads, **Then** a message indicates no research targets remain and suggests purchasing new BPOs.

---

### User Story 5 - Configurable Thresholds and Simulation (Priority: P3)

The user can configure profitability thresholds (minimum ISK/hr per slot, daily income goal, manufacturing slot count) and use "Simulate Next Phase" to preview what their portfolio would look like after advancing. All threshold changes take effect when the user clicks "Refresh Analysis".

**Why this priority**: Configuration and simulation are power-user features that enhance decision-making but are not required for the core ranking and recommendation flow.

**Independent Test**: Can be tested by adjusting threshold values, clicking Refresh, and verifying that phase completion status, blueprint highlighting, and recommendations all update accordingly.

**Acceptance Scenarios**:

1. **Given** the user changes the minimum ISK/hr threshold from 25M to 50M and clicks "Refresh Analysis", **When** the page recalculates, **Then** fewer blueprints qualify as "profitable" and phase completion may change.
2. **Given** the user clicks "Simulate Next Phase", **When** the simulation loads, **Then** all sections display as if the user were in the next phase, without actually advancing.
3. **Given** the user sets manufacturing slots to 15 (from default 11) and clicks "Refresh Analysis", **When** phase completion recalculates, **Then** the slot-based trigger requires ≥ ceil(15 × 9/11) profitable items.

---

### Edge Cases

- What happens when the user owns zero blueprints? → Empty state with guidance to acquire first BPOs.
- What happens when market data is unavailable for a station? → Affected items show "Price unavailable" and are excluded from ISK/hr ranking.
- What happens when a blueprint's product has zero market volume? → Item is flagged as "Low liquidity" and ranked lower.
- What happens when the user's skills change mid-session? → Skill data is refreshed on page load; mid-session changes appear on next navigation.
- What happens when the user manually advances past Phase 5? → "Advance Phase" button is disabled at Phase 5.
- What happens when phase data references type IDs not in the blueprint database? → Those items are silently excluded from phase counts.
- What happens when ESI rate limits are hit during refresh? → Show user-friendly message ("Rate limit hit – retrying in X seconds") and retry automatically.
- What happens when the user has more than 300 blueprints? → Show warning ("Large portfolio – consider filtering") before proceeding with refresh.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST display a protected page at `/portfoliooptimizer` accessible only to authenticated users.
- **FR-002**: System MUST calculate ISK/hr for each owned blueprint including system cost index fees, buying broker fee, selling broker fee, sales tax, material costs (highest buy order at the procurement station), and product revenue (lowest sell order at the selling hub). All pricing MUST be station-level.
- **FR-003**: System MUST sort owned blueprints by ISK/hr descending and allow re-sorting by other columns (name, profit margin, daily volume). The ranking table shows blueprints from all phases.
- **FR-004**: System MUST visually highlight blueprints belonging to the user's current production phase.
- **FR-005**: System MUST exclude blueprints the user lacks required skills to manufacture (skill gating).
- **FR-006**: System MUST define five static production phases with fixed type ID assignments:
  - Phase 1: T1 Frigate Foundation (T1 frigates + small modules/rigs/ammo)
  - Phase 2: T1 Destroyer Expansion (T1 destroyers + small modules/rigs)
  - Phase 3: T1 Cruiser & Battlecruiser Tier (T1 cruisers + battlecruisers + medium modules/rigs)
  - Phase 4: Battleship Tier (T1 battleships + large modules/rigs/ammo)
  - Phase 5: Capital & Advanced Entry (capitals + T2 entry)
- **FR-007**: System MUST trigger phase completion when ≥ ceil(N × 9/11) manufacturing slots can be filled with profitable items from the current phase, where N is the configured slot count and "profitable" means ISK/hr ≥ user-configurable minimum (default 25M ISK/hr).
- **FR-008**: System MUST use a secondary fallback phase trigger when total daily potential income exceeds a user-configurable daily goal (default 750M ISK/day).
- **FR-009**: System MUST provide a manual "Advance Phase" override button. Manual phase overrides MUST be persisted in local storage and survive across sessions. The auto-calculated phase serves as the baseline when no override is set.
- **FR-010**: System MUST display recommended BPO purchases scoped to the current phase (unowned BPOs). Once the phase advances (auto or manual), recommendations shift to the next phase's unowned BPOs. Each recommendation shows both NPC seeded price and player market lowest sell price (region-wide, not station-restricted), projected post-research ISK/hr, ROI percentage, and payback period.
- **FR-011**: System MUST display 5–10 research queue recommendations ordered by projected ISK/hr improvement from additional ME/TE research.
- **FR-012**: System MUST provide ME/TE what-if sliders. Changes take effect only when the user clicks "Refresh Analysis".
- **FR-013**: System MUST provide a single procurement station selector and a single selling hub selector. Changes take effect only when the user clicks "Refresh Analysis".
- **FR-014**: System MUST provide a "Simulate Next Phase" projection that previews all sections as if the user were in the next phase.
- **FR-015**: System MUST allow users to configure: minimum ISK/hr per slot (default 25M), daily income goal (default 750M), manufacturing slot count (default 11), and manufacturing system for cost index calculation (default: Jita). Changes take effect only when the user clicks "Refresh Analysis".
- **FR-016**: System MUST allow users to input three global fee percentages: buying broker fee % (applies to procurement station materials), selling broker fee % (applies to selling hub products), and sales tax % (applies to selling hub products). Changes take effect only when the user clicks "Refresh Analysis".
- **FR-017**: System MUST persist station/hub selections, broker/tax percentages, and portfolio configuration thresholds in browser local storage so they survive page reloads and re-authentication.
- **FR-018**: System MUST provide a single prominent "Refresh Analysis" button that triggers full re-evaluation of all ISK/hr rankings, phase completion status, BPO purchase recommendations, research queue suggestions, and all price-dependent values. The button MUST show a loading spinner while calculating and be disabled during refresh.
- **FR-019**: System MUST cache all market/price data with 5-minute sliding expiration per station/region + type_id.
- **FR-020**: System MUST limit concurrent ESI calls to 20 maximum during refresh.
- **FR-021**: System MUST show a user-friendly retry message when ESI rate limits are hit during refresh ("Rate limit hit – retrying in X seconds").
- **FR-022**: System MUST warn users when their portfolio exceeds ~200–300 blueprints ("Large portfolio – consider filtering") before proceeding with refresh.

### Key Entities

- **Production Phase**: A named tier in the industrialist progression (1–5) containing a fixed set of candidate type IDs. Has a name, ordinal position, and completion status.
- **Blueprint Ranking Entry**: An owned blueprint enriched with realistic ISK/hr, profit margin, phase membership, skill eligibility, and what-if projections at different ME/TE levels.
- **BPO Purchase Recommendation**: An unowned blueprint from the current (or next, post-advancement) phase with NPC seeded price, player market lowest sell price (region-wide), projected ISK/hr, ROI, and payback period.
- **Research Recommendation**: An owned under-researched blueprint with projected ISK/hr gain from additional ME/TE research.
- **Portfolio Configuration**: User-configurable thresholds including minimum ISK/hr per slot, daily income goal, manufacturing slot count, procurement station, selling hub, and global broker/tax percentages.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can identify their most profitable blueprint within 5 seconds of clicking "Refresh Analysis".
- **SC-002**: ISK/hr calculations match manual spreadsheet calculations within 1% margin (accounting for real-time price fluctuations).
- **SC-003**: Users can determine which BPOs to purchase next within 30 seconds of viewing the page.
- **SC-004**: Phase progression accurately reflects the user's manufacturing readiness — advancing only when the slot-fill criteria are genuinely met.
- **SC-005**: Full refresh completes within 10 seconds for portfolios of ≤ 200 blueprints.
- **SC-006**: 100% of displayed blueprints are buildable by the user's current skills (no false positives from skill gating).
- **SC-007**: Research recommendations, when followed, result in measurable ISK/hr improvement for the targeted blueprints.
- **SC-008**: Market data cache prevents redundant ESI calls within the 5-minute window, reducing API load.

## Assumptions

- Manufacturing slot count defaults to 11 but is user-configurable; this accommodates characters with varying levels of the Mass Production / Advanced Mass Production skills.
- The phase completion formula generalizes: for N slots, completion requires ≥ ceil(N × 9/11) profitable items. This maintains the ~82% utilization ratio at any slot count.
- Phase definitions and their associated type IDs are static embedded data that ships with the application. Updates to phases require an application update.
- "Fully researched" means ME 10 / TE 20 for T1 blueprints (the maximum research levels).
- BPO purchase prices use region-wide orders (not station-restricted) and display both NPC seeded price and player market lowest sell price, since BPOs are infrequent purchases where extra travel is trivial.
- Payback period is calculated as: BPO purchase price / (projected daily profit from manufacturing the item).
- ROI is calculated as: (projected daily profit × 30 days) / BPO purchase price × 100%.
- The system reuses the existing authentication flow — users must be logged in via EVE SSO to access the page.
- System cost index is fetched for the user-configured manufacturing system (default: Jita), persisted in local storage.
- All controls (stations, sliders, thresholds, fees) update local state immediately but trigger no computation until the user clicks "Refresh Analysis".
