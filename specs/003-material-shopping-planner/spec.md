# Feature Specification: Material Shopping List Planner

**Feature Branch**: `003-material-shopping-planner`
**Created**: 2026-03-15
**Status**: Draft
**Input**: User description: "Material Shopping List Planner (Batch Production Resource Calculator) — a page where logged-in industrialists can select multiple owned blueprints, set production runs, toggle component production, and see an aggregated shopping list with procurement costs."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Select Blueprints and Generate Material List (Priority: P1)

As a logged-in industrialist, I want to select one or more of my owned blueprints, set the number of runs for each, and generate an aggregated shopping list of all raw materials needed, so that I can see exactly what I need to buy for a production batch.

**Why this priority**: This is the core value proposition — without material list generation, the entire feature has no purpose. Every other story builds on top of this capability.

**Independent Test**: Can be fully tested by logging in, selecting blueprints, setting runs, clicking "Generate List", and verifying the aggregated material quantities are correct. Delivers immediate value as a material calculator even without cost data.

**Acceptance Scenarios**:

1. **Given** I am logged in and have blueprints, **When** I navigate to "/productionplanner", **Then** I see a list of all my character's blueprints with their name, ME/TE, type (BPO/BPC), and available runs.
2. **Given** I see my blueprint list, **When** I select multiple blueprints via checkboxes and set run counts, **Then** each selected blueprint shows a numeric input for runs (defaulting to 1).
3. **Given** I have selected blueprints with run counts, **When** I click "Generate List", **Then** I see an aggregated shopping list table showing Material Name and Quantity Needed for all selected blueprints combined, with duplicate materials merged and quantities summed.
4. **Given** a BPC with limited runs remaining, **When** I set runs exceeding the available amount, **Then** the system caps or warns me about the maximum runs available.
5. **Given** I have no blueprints on my character, **When** I visit the page, **Then** I see an appropriate empty state message.

---

### User Story 2 - Recursive Component Production (Priority: P1)

As an industrialist who owns intermediate component blueprints, I want to toggle "Produce Components" per blueprint so that the shopping list substitutes intermediate materials with their sub-materials when I own the relevant blueprints, reducing my list to only raw materials I actually need to purchase.

**Why this priority**: This is the key differentiator from a simple material lookup. Recursive expansion is what makes batch planning powerful and eliminates manual spreadsheet work for complex production chains.

**Independent Test**: Can be tested by selecting a blueprint that requires an intermediate component (e.g., a ship requiring modules), toggling "Produce Components" on, and verifying that the intermediate component is replaced by its raw materials in the shopping list — but only when the character owns a blueprint for that component.

**Acceptance Scenarios**:

1. **Given** I select a blueprint that requires intermediate components, **When** I toggle "Produce Components" on and I own blueprints for those components, **Then** the shopping list replaces each intermediate component with its raw sub-materials.
2. **Given** I toggle "Produce Components" on but do NOT own a blueprint for an intermediate component, **Then** that component remains in the shopping list as-is (it must be purchased).
3. **Given** a multi-level production chain (component A requires component B, which requires raw materials), **When** I toggle "Produce Components" and own blueprints for both A and B, **Then** the system recursively expands all levels down to raw materials.
4. **Given** I toggle "Produce Components" off for a blueprint, **When** I generate the list, **Then** only the top-level materials for that blueprint are shown (no recursive expansion).

---

### User Story 3 - Procurement Cost Estimation (Priority: P2)

As an industrialist planning a production batch, I want to see estimated market costs for each material in my shopping list based on a selected trade hub region, so that I can estimate total procurement spend before committing to the batch.

**Why this priority**: Cost data enhances decision-making but is not required for the core material planning workflow. The shopping list is independently useful without costs.

**Independent Test**: Can be tested by generating a material list, selecting a region from the dropdown, and verifying that cost and volume columns populate with market data. Changing region should only refresh costs, not recalculate quantities.

**Acceptance Scenarios**:

1. **Given** I have a generated shopping list, **When** I select a region (defaulting to The Forge), **Then** each material row shows Estimated Cost and Volume (m³), plus a total ISK amount at the bottom.
2. **Given** I change the selected region, **When** cost data refreshes, **Then** only the cost column and total update — the material list and quantities remain unchanged.
3. **Given** market data is unavailable for a material, **When** costs are displayed, **Then** that material shows "N/A" or similar indicator without breaking the total calculation.
4. **Given** a large shopping list, **When** market data is loading, **Then** I see loading indicators on the cost column while quantities remain visible.

---

### User Story 4 - Export and Clipboard Actions (Priority: P2)

As an industrialist, I want to export my shopping list as CSV or copy it to clipboard so that I can share it with corp members or paste it into other tools.

**Why this priority**: Export is a convenience feature that amplifies the value of the core list but is not required for planning.

**Independent Test**: Can be tested by generating a list and clicking "Export CSV" (verifying a valid CSV downloads) or "Copy to Clipboard" (verifying clipboard contains the formatted list).

**Acceptance Scenarios**:

1. **Given** I have a generated shopping list, **When** I click "Export CSV", **Then** a CSV file downloads containing all material names, quantities, costs (if loaded), and volumes.
2. **Given** I have a generated shopping list, **When** I click "Copy to Clipboard", **Then** the list is copied in a readable text format.
3. **Given** no shopping list has been generated, **When** I look at the export buttons, **Then** they are disabled or hidden.

---

### User Story 5 - Material Source Details (Priority: P3)

As an industrialist reviewing a large aggregated list, I want to expand each material row to see which blueprint(s) require it and how much each contributes, so that I can understand where demand comes from.

**Why this priority**: This is a drill-down/diagnostic feature that aids understanding but is not needed for basic batch planning.

**Independent Test**: Can be tested by generating a list from multiple blueprints sharing a common material, expanding that material's detail row, and verifying it shows a breakdown by contributing blueprint.

**Acceptance Scenarios**:

1. **Given** an aggregated shopping list with a material required by multiple blueprints, **When** I expand the detail row, **Then** I see a breakdown showing each contributing blueprint and its quantity contribution.
2. **Given** a material required by only one blueprint, **When** I expand its detail, **Then** I see that single blueprint listed.

---

### Edge Cases

- What happens when a user has hundreds of blueprints? The blueprint list includes a text search filter (FR-003a) and should remain performant with large inventories.
- How does the system handle circular dependencies in recursive component expansion? The system must detect and break cycles to avoid infinite recursion.
- What happens if the user's session expires mid-planning? The system should handle authentication errors gracefully and preserve selection state where possible.
- What happens when ME (Material Efficiency) is applied? Material quantities must account for ME reductions on each blueprint.
- What if the same intermediate component is required by multiple selected blueprints with "Produce Components" toggled? Quantities must aggregate correctly across all sources before recursive expansion.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST require authentication to access the production planner page — unauthenticated users are redirected to login.
- **FR-002**: System MUST display all character-owned blueprints with name, Material Efficiency (ME), Time Efficiency (TE), type (BPO or BPC), and available runs.
- **FR-003**: System MUST allow multi-selection of blueprints via checkboxes.
- **FR-003a**: System MUST provide a text search filter above the blueprint list to allow users to filter blueprints by name.
- **FR-004**: System MUST provide a numeric input for each selected blueprint to specify number of production runs (default: 1).
- **FR-005**: System MUST provide a per-blueprint toggle for "Produce Components" to enable recursive material expansion.
- **FR-006**: System MUST generate an aggregated shopping list that merges duplicate materials across all selected blueprints and sums their quantities.
- **FR-007**: System MUST apply Material Efficiency (ME) reductions when calculating material quantities for each blueprint.
- **FR-008**: When "Produce Components" is toggled on, the system MUST recursively substitute intermediate materials with their sub-materials, but only for intermediates where the character owns a corresponding blueprint. Each sub-component's own ME value MUST be applied independently during recursive expansion.
- **FR-009**: System MUST detect and prevent circular dependencies during recursive material expansion.
- **FR-010**: System MUST provide a region selector (defaulting to The Forge / region ID 10000002) for cost estimation.
- **FR-011**: System MUST display estimated per-material cost (using lowest sell order price in the selected region), volume (m³), and a total ISK amount when a region is selected.
- **FR-012**: Changing the selected region MUST only refresh cost data — material quantities and the shopping list structure must not be recalculated.
- **FR-013**: System MUST provide "Generate List", "Export CSV", "Copy to Clipboard", and "Clear Selection" action buttons.
- **FR-014**: System MUST show appropriate loading states during data fetches and an empty state when no blueprints are selected.
- **FR-015**: System MUST cache blueprint data on page load to avoid redundant fetches during the planning session.
- **FR-016**: System MUST group materials by category where possible, with totals displayed at the bottom of the shopping list.
- **FR-017**: Each material row MUST be expandable to show which blueprint(s) require it and their individual quantity contributions.

### Key Entities

- **Blueprint Selection**: Represents a user's choice of a specific owned blueprint combined with a run count and produce-components flag. Key attributes: blueprint identity, run count, ME/TE values, type (BPO/BPC), produce-components toggle.
- **Material Tree**: A recursive structure representing the full material breakdown for a blueprint. When "Produce Components" is active, intermediate nodes expand into child material requirements; leaf nodes are raw materials to purchase.
- **Shopping List Item**: A single aggregated row in the final output — material identity, total quantity needed, category, volume, estimated cost, and a list of contributing blueprint sources with per-source quantities.
- **Trade Hub Region**: A market region used for cost estimation — region identity and display name. Default: The Forge (10000002). Other common hubs: Domain, Sinq Laison, Metropolis, Heimatar.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can generate a complete aggregated shopping list from selected blueprints in under 5 seconds for up to 20 simultaneous blueprint selections.
- **SC-002**: Recursive component expansion correctly resolves multi-level production chains (at least 3 levels deep) with zero material quantity errors.
- **SC-003**: Changing the trade hub region refreshes cost data without recalculating the material list, completing the cost update in under 3 seconds.
- **SC-004**: Users can complete the full workflow (select blueprints → set runs → generate list → view costs) in under 2 minutes for a typical 5-blueprint batch.
- **SC-005**: Exported CSV contains all visible shopping list data and can be opened correctly in standard spreadsheet applications.
- **SC-006**: Material Efficiency reductions are accurately applied, matching the game's ME formula within rounding tolerance.

## Clarifications

### Session 2026-03-15

- Q: Where does blueprint bill-of-materials data come from (ESI doesn't expose manufacturing inputs)? → A: Bundle the Static Data Export (SDE) as a local dataset for bill-of-materials lookups.
- Q: Which ME value applies when recursively expanding sub-components? → A: Each sub-component blueprint's own ME value is used (realistic per-job calculation).
- Q: What pricing strategy for market cost estimation? → A: Lowest sell order price (immediate buy price).
- Q: How should users find blueprints in large inventories? → A: Text search filter above the blueprint list (type-to-filter by name).

## Assumptions

- Blueprint material requirements (bill of materials) are sourced from the EVE Online Static Data Export (SDE), bundled as a local dataset. The SDE is the canonical CCP-maintained source for manufacturing inputs, avoiding runtime dependency on third-party APIs.
- The existing market data service provides per-item pricing by region and can be reused for cost estimation.
- The region list for cost estimation will use well-known trade hub regions (The Forge, Domain, Sinq Laison, Metropolis, Heimatar) rather than all 60+ regions, to keep the UX simple.
- ME formula follows EVE Online's standard: `max(runs, ceil(runs * base_quantity * (1 - ME/100)))` for each material.
- Users will typically select fewer than 20 blueprints per batch; performance targets are based on this assumption.
- Blueprint data is fetched once on page load and cached for the duration of the session — users do not need real-time updates to their blueprint inventory while planning.

## Out of Scope

- Manufacturing job scheduling or time estimation
- Reaction formulas or reprocessing calculations
- Corporation-level blueprint libraries
- Shipping, hauling, or logistics cost estimation
- Skill-based production feasibility warnings
- "What-if" ME/TE override simulations
- Public blueprint browsing (non-owned blueprints)
- Drag-and-drop reordering of blueprint selections
