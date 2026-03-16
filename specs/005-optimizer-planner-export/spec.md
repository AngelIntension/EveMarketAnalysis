# Feature Specification: Optimizer-to-Planner Export Integration (One-Click Batch Production)

**Feature Branch**: `005-optimizer-planner-export`
**Created**: 2026-03-16
**Status**: Draft
**Input**: User description: "Export selected blueprints from Portfolio Optimizer rankings table to Production Planner with run counts, via localStorage handoff"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Select and Export Blueprints from Optimizer (Priority: P1)

As an industrialist viewing the Portfolio Optimizer rankings table, I want to check off profitable blueprints and click a single button to export them with their run counts to the Production Planner, so I can quickly move from analysis to production planning without re-entering data.

**Why this priority**: This is the core value proposition — bridging the gap between the two pages. Without this, users must manually find and configure each blueprint in the planner after analyzing them in the optimizer.

**Independent Test**: Can be fully tested by selecting blueprints in the optimizer table, clicking Export, and verifying the data appears in localStorage. Delivers value even before the planner import side is built (data is staged for later consumption).

**Acceptance Scenarios**:

1. **Given** the optimizer rankings table is populated with analyzed blueprints, **When** the user checks the selection checkbox on 3 blueprints, **Then** the Export button label updates to "Export 3 to Planner" and becomes enabled.
2. **Given** multiple blueprints are checked, **When** the user clicks the Export button, **Then** a payload containing each selected blueprint's type ID, name, and current runs value is stored in localStorage under `pendingProductionBatch`.
3. **Given** the export payload has been stored, **When** the export completes, **Then** the Production Planner opens in a new browser tab at `/productionplanner`.
4. **Given** no blueprints are checked, **When** the user views the Export button, **Then** the button is disabled with a tooltip "Select blueprints to export".

---

### User Story 2 - Auto-Import Exported Blueprints in Production Planner (Priority: P1)

As an industrialist who just exported blueprints from the optimizer, I want the Production Planner to automatically detect and import those selections when it loads, so I can immediately generate a shopping list without manual setup.

**Why this priority**: This completes the export-import loop. Without auto-import, the export is useless — users would still need to manually select blueprints in the planner.

**Independent Test**: Can be tested by manually placing a well-formed JSON payload in localStorage under `pendingProductionBatch`, loading the Production Planner page, and verifying the blueprints are auto-selected with correct run counts.

**Acceptance Scenarios**:

1. **Given** a `pendingProductionBatch` key exists in localStorage with valid blueprint data, **When** the Production Planner page loads and fetches the user's blueprints, **Then** the matching blueprints are automatically checked and their run inputs are set to the exported values.
2. **Given** the import succeeds with 5 blueprints, **When** the auto-selection completes, **Then** a notification appears: "Imported 5 blueprints from Optimizer" and the localStorage key is removed.
3. **Given** the pending batch contains a blueprint type ID that does not exist in the user's current blueprint list, **When** the import runs, **Then** that entry is silently skipped and the remaining blueprints are still imported.
4. **Given** the import completes, **When** the user clicks "Generate Shopping List", **Then** the shopping list is generated correctly using the imported selections and run counts (existing planner functionality unchanged).

---

### User Story 3 - Merge Imported Blueprints with Existing Planner Selections (Priority: P2)

As an industrialist who already has some blueprints selected in the planner, I want imported blueprints to merge with my existing selections rather than replacing them, so I don't lose work I've already set up.

**Why this priority**: This is important for power users who may iterate between the optimizer and planner, but most users will start with a clean planner state. The merge behavior prevents data loss but is secondary to the core export-import flow.

**Independent Test**: Can be tested by pre-selecting blueprints in the planner, then loading a pending batch from localStorage, and verifying both the pre-existing and newly imported selections are present.

**Acceptance Scenarios**:

1. **Given** the planner has blueprints A and B already selected, **When** a pending batch containing blueprints B and C is imported, **Then** blueprints A, B, and C are all selected — A retains its original runs, B's runs are updated to the imported value, and C is newly selected.
2. **Given** the planner has blueprint X selected with 5 runs, **When** a pending batch imports blueprint X with 10 runs, **Then** blueprint X's runs input is updated to 10 (imported value takes precedence).

---

### Edge Cases

- **Zero or invalid runs in selected optimizer rows**: If a selected blueprint has 0 or blank runs, the export auto-sets runs to 1 for that entry (prevents invalid shopping list generation).
- **Export while table data is stale**: The export uses whatever data is currently displayed in the table — no additional fetch is triggered. The user is responsible for refreshing analysis data before exporting.
- **Page reload on planner before import**: The pending batch persists in localStorage until consumed. Reloading the planner page will re-trigger the import.
- **Multiple rapid exports**: Each export overwrites the previous `pendingProductionBatch` key. Only the most recent export is imported.
- **Browser localStorage disabled or full**: If localStorage is unavailable, the export button shows an error notification rather than failing silently.
- **Planner opened without pending batch**: Normal planner behavior — no import notification shown, no changes to existing selections.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The optimizer rankings table MUST display a new checkbox column allowing multi-selection of blueprint rows.
- **FR-002**: The optimizer page MUST display an "Export to Planner" button that shows the count of selected items (e.g., "Export 7 to Planner") and is disabled when no items are selected.
- **FR-003**: When disabled, the Export button MUST show a tooltip: "Select blueprints to export".
- **FR-004**: Clicking the Export button MUST create a payload containing each selected blueprint's type ID, produced item name, and current runs value.
- **FR-005**: If a selected blueprint has zero or blank runs, the system MUST auto-set runs to 1 in the export payload.
- **FR-006**: The export payload MUST be serialized to JSON and stored in localStorage under the key `pendingProductionBatch`.
- **FR-007**: After storing the payload, the system MUST open the Production Planner page (`/productionplanner`) in a new browser tab.
- **FR-008**: On page load, the Production Planner MUST check localStorage for a `pendingProductionBatch` key.
- **FR-009**: If a pending batch exists, the planner MUST auto-select matching blueprints (by type ID) and set their run count inputs to the exported values.
- **FR-010**: After a successful import, the planner MUST remove the `pendingProductionBatch` key from localStorage.
- **FR-011**: After a successful import, the planner MUST display a notification: "Imported X blueprints from Optimizer" (where X is the count of successfully matched items).
- **FR-012**: If a type ID in the pending batch does not match any blueprint in the planner's list, that entry MUST be silently skipped.
- **FR-013**: Imported blueprints MUST merge with any existing planner selections — new entries are added, and conflicting entries (same type ID) have their runs overwritten by the imported value.
- **FR-014**: All existing planner functionality (shopping list generation, cost calculation, CSV export, clipboard copy) MUST remain unchanged and work correctly with imported selections.

### Key Entities

- **Production Batch Export**: A collection of blueprint run specifications intended for transfer from the optimizer to the planner. Contains a list of individual blueprint entries.
- **Blueprint Run Export Entry**: A single blueprint's export data comprising the blueprint type identifier, the produced item name, and the number of manufacturing runs.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can move from optimizer analysis to a planner shopping list in under 30 seconds (previously required manual re-entry of each blueprint).
- **SC-002**: 100% of exported blueprints that exist in the user's blueprint collection are correctly auto-selected in the planner with accurate run counts.
- **SC-003**: Users with pre-existing planner selections retain all their selections after an import (no data loss from merge).
- **SC-004**: The export-import cycle works reliably across page reloads — a pending batch persists until consumed.
- **SC-005**: The entire feature works without any additional server calls beyond normal page loading (no new API endpoints required).

## Assumptions

- The optimizer's "Runs" column value (which is calculated from production duration and time per run) is the appropriate value to export. Users can adjust runs in the planner after import if needed.
- The blueprint type ID (not produced type ID) is the correct identifier for matching between optimizer and planner, since both pages use blueprint type IDs as their primary key for selection.
- The notification for successful import can be a simple inline banner/toast consistent with the application's existing dark theme — no formal notification library is required.
- The "Produce Components" flag on imported blueprints defaults to unchecked (off), since the optimizer does not track this preference. Users can enable it per-blueprint in the planner after import.

## Out of Scope

- Bidirectional sync between the optimizer and planner pages
- Persistent cross-session batch history (localStorage is one-time use per export)
- Automatic import without user-visible notification
- Exporting BPO/research recommendations from the optimizer (only the main rankings table)
- Modifying the optimizer's existing Runs calculation logic
