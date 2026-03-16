# Research: Optimizer-to-Planner Export Integration

**Date**: 2026-03-16 | **Branch**: `005-optimizer-planner-export`

## Research Topics

### 1. localStorage Cross-Tab Communication Pattern

**Decision**: Use `localStorage.setItem()` on the optimizer side and `localStorage.getItem()` on the planner side, with the planner consuming and removing the key on page load after blueprint data is fetched.

**Rationale**: Both pages already use localStorage (optimizer: `portfolioConfig`) and sessionStorage (planner: `pp-selections`). The cross-tab handoff via localStorage is the simplest approach — no `BroadcastChannel`, `SharedWorker`, or `storage` event listener needed because the planner reads on load rather than reacting to real-time changes.

**Alternatives considered**:
- `BroadcastChannel` API — more complex, requires both tabs to be open simultaneously, overkill for a one-shot handoff
- URL query parameters — payload too large for URL length limits with many blueprints, exposes data in browser history
- `sessionStorage` — not shared across tabs, would not work since export opens a new tab
- Server-side session storage — adds unnecessary server round-trip and complexity

### 2. Planner Import Timing (When to Read localStorage)

**Decision**: Read localStorage after the planner's blueprint list has been fetched and rendered (inside the `loadBlueprints()` success callback), not on raw page load.

**Rationale**: The import needs to match exported type IDs against the planner's blueprint list DOM elements. If we read localStorage before blueprints are rendered, there are no `.bp-check` elements to programmatically check. The existing planner fetches blueprints via `fetch('/ProductionPlanner?handler=Blueprints')` and renders them in a callback — the import should execute after that render completes.

**Alternatives considered**:
- Read on `DOMContentLoaded` — too early, blueprints not yet fetched from server
- Use `MutationObserver` to watch for blueprint list population — unnecessarily complex
- Defer with `setTimeout` — fragile and race-condition-prone

### 3. Merge Strategy for Conflicting Selections

**Decision**: Import-wins merge — iterate over pending batch entries, check/select each matching blueprint, and set its runs input value. Existing selections not in the batch are left untouched. Conflicting type IDs have their runs overwritten by the imported value.

**Rationale**: The spec explicitly states "conflicting entries (same type ID) have their runs overwritten by the imported value" (FR-013). This is the simplest merge strategy and matches user intent — the optimizer's calculated runs reflect the latest analysis.

**Alternatives considered**:
- Additive merge (sum runs) — confusing, could produce unexpectedly large run counts
- User prompt per conflict — too much friction for a "one-click" feature
- Replace all (clear existing, import only batch) — would lose user's manual selections

### 4. Export Payload Schema

**Decision**: Simple flat JSON array under a wrapper object:
```json
{
  "items": [
    { "typeId": 691, "name": "Rifter", "runs": 5 },
    { "typeId": 11379, "name": "Raven", "runs": 2 }
  ]
}
```

**Rationale**: Matches the C# `ProductionBatchExport` record structure. The wrapper object allows future extensibility (e.g., adding export timestamp or source region) without breaking the schema. Using `typeId` (blueprint type ID) as the key matches the planner's `data-typeid` attribute on `.bp-check` elements.

**Alternatives considered**:
- Bare array (no wrapper) — works but less extensible
- Include ME/TE/isCopy in payload — unnecessary, planner already has this data from its own blueprint fetch
- Include `produceComponents` flag — optimizer doesn't track this, default to false in planner

### 5. Zero/Invalid Runs Handling

**Decision**: Clamp runs to minimum of 1 during export payload creation. If a row's runs value is 0, blank, or NaN, set it to 1 in the payload.

**Rationale**: The planner's runs input has `min="1"` — a value of 0 would be invalid. Clamping at export time is simpler than handling it at import time and ensures the payload is always valid.

**Alternatives considered**:
- Skip rows with invalid runs — would silently drop user selections, confusing
- Show validation error and block export — too disruptive for an edge case
- Clamp at import time instead — works but means invalid data in localStorage

### 6. Notification Style

**Decision**: Use an inline banner at the top of the blueprint list panel, auto-dismissing after 5 seconds, styled with the existing dark theme (Bootstrap `alert-info` with EVE-themed colors).

**Rationale**: Both pages already use Bootstrap for layout. A simple `alert` div inserted at the top of the blueprint list is consistent with the existing UI pattern. Auto-dismiss prevents the notification from permanently occupying space.

**Alternatives considered**:
- Browser `Notification` API — requires permission, overkill for in-app feedback
- Toast library (e.g., Toastr) — adds a new dependency, violates "no new NuGet packages" constraint
- Console.log only — not user-visible

### 7. C# Model vs JS-Only Contract

**Decision**: Create a C# `ProductionBatchExport` immutable record that mirrors the JSON schema, with unit tests for serialization round-trip. The actual serialization/deserialization happens in JavaScript, but the C# model serves as the canonical contract definition and enables testable merge logic.

**Rationale**: Constitution requires TDD and immutable records. The C# model provides a single source of truth for the JSON shape, catches schema drift, and satisfies the constitution's functional style requirements. A `ProductionBatchExportService` static class provides pure helper methods (payload creation, merge logic) that are independently testable.

**Alternatives considered**:
- JS-only with no C# model — violates constitution's TDD and immutability requirements
- Full server-side round-trip (post payload to server, redirect to planner) — unnecessary complexity, adds server calls
