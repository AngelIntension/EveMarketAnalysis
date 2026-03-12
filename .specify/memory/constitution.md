<!--
Sync Impact Report
===================
- Version change: 1.0.0 -> 1.1.0 (MINOR: new performance constraints)
- Modified principles: N/A
- Added to Performance & Infrastructure Constraints:
  - Parallel Dispatch: independent ESI calls must use Task.WhenAll
  - Bulk Endpoints First: prefer POST /universe/names over per-ID calls
  - Cache Immutability: never mutate cached data structures in place
- Removed sections: N/A
- Templates requiring updates:
  - .specify/templates/plan-template.md - ✅ no update needed
  - .specify/templates/spec-template.md - ✅ no update needed
  - .specify/templates/tasks-template.md - ✅ no update needed
  - .specify/templates/commands/*.md - ✅ no command files exist
- Follow-up TODOs: none
-->

# EveMarketAnalysis Constitution

## Core Principles

### I. Functional & Immutable Style

All new code MUST be written in a functional and immutable style
wherever the platform permits. This principle ensures predictable
data flow, easier testing, and fewer side-effect bugs across the
entire codebase.

- Prefer `record`, `record struct`, and `readonly struct` over
  mutable `class` types for data carriers.
- Use pure functions, method chaining, and expression-bodied
  members as the default coding idiom.
- Use `with` expressions for deriving modified copies; mark fields
  and properties `readonly` wherever possible.
- Collections MUST NOT be mutated after creation. Use
  `ImmutableArray`, `ImmutableList`, `FrozenDictionary`,
  or LINQ projections that return new collections.
- Avoid mutable state and side-effects. Exceptions are permitted
  ONLY where Blazor component lifecycle (e.g., `OnInitializedAsync`,
  `StateHasChanged`) or Kiota-generated client classes require
  mutable patterns.
- Comments MUST explain functional transformations when the intent
  is non-obvious.

### II. Sensitive Data Handling

All secrets, API keys, tokens, and credentials MUST be protected
from source-control exposure at every stage of development and
deployment.

- All secrets (e.g., ESI OAuth tokens, developer API keys) MUST be
  stored exclusively via `dotnet user-secrets`
  (`Microsoft.Extensions.Configuration.UserSecrets`).
- `Program.cs` MUST call `builder.Configuration.AddUserSecrets<T>()`
  (or equivalent) so secrets are available through `IConfiguration`.
- Secrets MUST NEVER appear in `appsettings.json`,
  `appsettings.Development.json`, source code literals, or
  environment-variable definitions committed to version control.
- Any PR or generated code that hard-codes, logs, or exposes a
  secret MUST be rejected or immediately refactored.
- `.gitignore` MUST include patterns that prevent accidental commit
  of secret stores or credential files.

### III. Test-Driven Development (NON-NEGOTIABLE)

Every feature and bug-fix MUST follow strict TDD. No production
code may be written without corresponding failing tests.

**Workflow (Red-Green-Refactor)**:
1. **Red** -- Write failing xUnit tests first.
2. **Green** -- Implement the minimal code to make tests pass.
3. **Refactor** -- Improve code quality while keeping tests green.

**Mandatory Test Stack**:
- **xUnit.net** (v2+) for the test framework.
- **FluentAssertions** for readable, expressive assertions.
- **Moq** for mocking dependencies.
- **AutoFixture** (with `AutoMoqCustomization`) for automatic
  test-data generation.

**Coverage Requirements**:
- 100% of new logic MUST have unit tests covering happy paths,
  edge cases, null inputs, and immutability guarantees.
- Integration tests MUST cover ESI API interactions, caching
  behavior, and cross-service data flow.
- Every task in `tasks.md` MUST list its tests before listing
  implementation steps.

## Performance & Infrastructure Constraints

All ESI API interactions and data-processing pipelines MUST respect
the following operational constraints:

- **Rate-Limit Compliance**: All ESI HTTP calls MUST respect the
  `X-ESI-Error-Limit-Remain` and `Retry-After` headers. Clients
  MUST implement back-off when approaching rate limits.
- **Caching**: Use `IMemoryCache` (or `IDistributedCache` when
  appropriate) to avoid redundant ESI requests. Cache durations
  MUST align with ESI cache expiry headers.
- **Parallel Dispatch**: Independent ESI calls MUST be dispatched
  concurrently (via `Task.WhenAll` or similar) rather than awaited
  sequentially. Service orchestrators MUST NOT serialize calls that
  have no data dependency on each other.
- **Bulk Endpoints First**: When resolving multiple IDs to names or
  metadata, PREFER bulk ESI endpoints (e.g., `POST /universe/names`)
  over per-ID calls (e.g., `GET /universe/types/{id}`). A single
  bulk call replaces hundreds of individual requests.
- **Cache Immutability**: Cached data structures MUST NOT be mutated
  after storage. If enrichment is needed (e.g., adding display names
  to a group mapping), build a separate data structure rather than
  modifying the cached one in place. Mutation of shared cache entries
  corrupts downstream consumers that depend on the original shape.
- **Purity of Wrappers**: Service classes that wrap the Kiota
  `ApiClient` MUST remain as pure as possible -- accept inputs,
  return immutable results, and push side-effects (HTTP, caching)
  to the boundary.
- **Solution Structure**: All code MUST stay within the existing
  two-project solution structure:
  - `EveMarketAnalysisClient/` -- ASP.NET Core 8 Razor Pages /
    Blazor web application.
  - `EveStableInfrastructureApiClient/` -- Kiota-generated ESI
    client (do not hand-edit).
  - Test projects MUST be added as separate `*.Tests` projects
    within the same solution.

## General Governance Rules

These rules apply across all specifications, plans, tasks, and
generated code.

- The constitution takes precedence over every other guideline.
  The AI agent MUST reject, flag, or refactor any output that
  violates Principles I -- III or the constraints above.
- All Blazor pages, Razor components, services, and ESI Kiota
  client wrappers MUST remain functionally pure where possible.
- Documentation and inline comments MUST explain functional
  transformations and the rationale behind immutability choices.
- Generated plans (`plan.md`) MUST include a Constitution Check
  gate that validates compliance before implementation begins.
- Generated task lists (`tasks.md`) MUST list test tasks before
  implementation tasks for every user story.

## Governance

This constitution is the supreme governance document for the
EveMarketAnalysis project. All specifications, plans, task lists,
code reviews, and AI-generated outputs MUST comply.

**Amendment Procedure**:
1. Propose the change with a rationale and impact assessment.
2. Update `constitution.md` with the new or revised text.
3. Increment the version per semantic versioning (see below).
4. Propagate changes to dependent templates and artifacts.

**Versioning Policy** (Semantic Versioning):
- **MAJOR**: Backward-incompatible principle removal or
  redefinition (e.g., dropping TDD requirement).
- **MINOR**: New principle or section added, or material
  expansion of existing guidance.
- **PATCH**: Clarifications, wording, typo fixes, or
  non-semantic refinements.

**Compliance Review**:
- Every PR MUST be verified against active principles before
  merge.
- The AI agent MUST perform a constitution compliance check
  at the start of `/speckit.plan` and `/speckit.tasks` workflows.
- Violations discovered post-merge MUST be tracked and
  remediated in the next sprint.

**Version**: 1.1.0 | **Ratified**: 2026-03-12 | **Last Amended**: 2026-03-12
