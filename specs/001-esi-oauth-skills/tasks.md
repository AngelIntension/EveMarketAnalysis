# Tasks: ESI OAuth2 Authentication with PKCE + Character Skills Summary

**Input**: Design documents from `/specs/001-esi-oauth-skills/`
**Prerequisites**: plan.md (required), spec.md (required), research.md, data-model.md, quickstart.md

**Tests**: TDD is mandatory per constitution. Tests MUST be written first and FAIL before implementation.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization, test project creation, NuGet packages, shared model records

- [X] T001 Add project reference to EveStableInfrastructureApiClient and add NuGet packages (System.IdentityModel.Tokens.Jwt, Microsoft.IdentityModel.Protocols.OpenIdConnect, System.Collections.Immutable, Microsoft.Kiota.Abstractions, Microsoft.Kiota.Http.HttpClientLibrary) in EveMarketAnalysisClient/EveMarketAnalysisClient.csproj
- [X] T002 Create test project EveMarketAnalysisClient.Tests/ with xunit, FluentAssertions, Moq, AutoFixture, AutoFixture.AutoMoq, Microsoft.NET.Test.Sdk, xunit.runner.visualstudio, Microsoft.AspNetCore.Mvc.Testing and add to EveMarketAnalysis.sln
- [X] T003 [P] Create EsiOptions configuration record (ClientId, RedirectUri, Scopes) in EveMarketAnalysisClient/Configuration/EsiOptions.cs
- [X] T004 [P] Create EsiOAuthMetadata record in EveMarketAnalysisClient/Models/EsiOAuthMetadata.cs
- [X] T005 [P] Create PkceParameters record in EveMarketAnalysisClient/Models/PkceParameters.cs
- [X] T006 [P] Create EsiTokenSet record in EveMarketAnalysisClient/Models/EsiTokenSet.cs
- [X] T007 [P] Create CharacterSkill record in EveMarketAnalysisClient/Models/CharacterSkill.cs
- [X] T008 [P] Create SkillQueueEntry record in EveMarketAnalysisClient/Models/SkillQueueEntry.cs
- [X] T009 [P] Create SkillGroupSummary record in EveMarketAnalysisClient/Models/SkillGroupSummary.cs
- [X] T010 [P] Create CharacterSummary record in EveMarketAnalysisClient/Models/CharacterSummary.cs
- [X] T011 Wire up user-secrets configuration in EveMarketAnalysisClient/Program.cs (AddUserSecrets, bind EsiOptions from IConfiguration)

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core services that ALL user stories depend on -- OAuth metadata discovery, PKCE generation, rate-limit handling

**CRITICAL**: No user story work can begin until this phase is complete

### Tests for Foundational Phase

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T012 [P] Write PkceService unit tests (verifier length/charset, challenge SHA256+Base64Url derivation, state randomness, known RFC 7636 test vectors) in EveMarketAnalysisClient.Tests/Unit/PkceServiceTests.cs
- [X] T013 [P] Write EsiOAuthMetadataService tests (parses metadata JSON correctly, caches after first fetch, throws on unreachable endpoint, validates S256 in code_challenge_methods) in EveMarketAnalysisClient.Tests/Services/EsiOAuthMetadataServiceTests.cs
- [X] T014 [P] Write EsiRateLimitHandler tests (passes through when error limit high, delays when limit low, respects Retry-After header value) in EveMarketAnalysisClient.Tests/Services/EsiRateLimitHandlerTests.cs
- [X] T015 [P] Write EsiTokenSet record tests (immutability, with expressions, IsExpired returns true/false based on ExpiresAt) in EveMarketAnalysisClient.Tests/Unit/EsiTokenSetTests.cs

### Implementation for Foundational Phase

- [X] T016 Create IEsiOAuthMetadataService interface in EveMarketAnalysisClient/Services/Interfaces/IEsiOAuthMetadataService.cs
- [X] T017 Implement PkceService (static pure functions: GenerateCodeVerifier, GenerateCodeChallenge, GenerateState) in EveMarketAnalysisClient/Services/PkceService.cs
- [X] T018 Implement EsiOAuthMetadataService (fetches + caches metadata from discovery URL using HttpClient and IMemoryCache) in EveMarketAnalysisClient/Services/EsiOAuthMetadataService.cs
- [X] T019 Implement EsiRateLimitHandler (DelegatingHandler checking X-ESI-Error-Limit-Remain and Retry-After headers) in EveMarketAnalysisClient/Middleware/EsiRateLimitHandler.cs
- [X] T020 Register foundational services in DI (EsiOAuthMetadataService, HttpClient for ESI with EsiRateLimitHandler, IMemoryCache, EsiOptions) in EveMarketAnalysisClient/Program.cs

**Checkpoint**: Foundation ready -- PKCE generation, metadata discovery, and rate-limit handling all verified. User story implementation can now begin.

---

## Phase 3: User Story 1 - Login with EVE Online Character (Priority: P1) MVP

**Goal**: User can click "Login with EVE", be redirected to EVE SSO with PKCE parameters, exchange the auth code for tokens on callback, and see their character name in the navbar.

**Independent Test**: Complete the login flow end-to-end and verify the character name appears in the navbar.

### Tests for User Story 1

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T021 [P] [US1] Write EsiTokenService tests (builds correct token exchange request body, asserts no client_secret param is present in request (FR-005), validates JWT with correct issuer/audience matching client ID/expiration, rejects expired JWT, rejects wrong issuer, extracts character ID and name from sub/name claims, handles HTTP error responses) in EveMarketAnalysisClient.Tests/Services/EsiTokenServiceTests.cs
- [X] T022 [P] [US1] Write Login page tests (generates redirect URL with correct authorization_endpoint, includes code_challenge with S256 method, includes state parameter, includes correct scopes, stores verifier+state in temp cookie) in EveMarketAnalysisClient.Tests/Pages/LoginPageTests.cs
- [X] T023 [P] [US1] Write Callback page tests (validates state matches stored value, rejects mismatched state, exchanges code for tokens via EsiTokenService, signs in user with auth cookie on success, displays error on denied consent, displays error on exchange failure) in EveMarketAnalysisClient.Tests/Pages/CallbackPageTests.cs

### Implementation for User Story 1

- [X] T024 [US1] Create IEsiTokenService interface in EveMarketAnalysisClient/Services/Interfaces/IEsiTokenService.cs
- [X] T025 [US1] Implement EsiTokenService (ExchangeCodeAsync posts to token_endpoint with code+verifier, ValidateJwtAsync validates signature via JWKS + issuer + audience + expiry, ParseCharacterFromToken extracts character ID and name) in EveMarketAnalysisClient/Services/EsiTokenService.cs
- [X] T026 [US1] Configure cookie authentication in Program.cs (AddAuthentication + AddCookie with persistent cookie, Data Protection encryption, login/logout paths)
- [X] T027 [US1] Create Auth/Login Razor Page (generates PKCE params, stores verifier+state in encrypted temp cookie, redirects to authorization_endpoint with query params) in EveMarketAnalysisClient/Pages/Auth/Login.cshtml and EveMarketAnalysisClient/Pages/Auth/Login.cshtml.cs
- [X] T028 [US1] Create Auth/Callback Razor Page (reads code+state from query, validates state against temp cookie, calls EsiTokenService.ExchangeCodeAsync, validates JWT, creates ClaimsPrincipal, calls HttpContext.SignInAsync, redirects to home) in EveMarketAnalysisClient/Pages/Auth/Callback.cshtml and EveMarketAnalysisClient/Pages/Auth/Callback.cshtml.cs
- [X] T029 [US1] Update _Layout.cshtml navbar to show "Login with EVE" button when unauthenticated and character name when authenticated (read from User.Identity claims) in EveMarketAnalysisClient/Pages/Shared/_Layout.cshtml
- [X] T030 [US1] Register EsiTokenService in DI and add UseAuthentication + UseAuthorization middleware in correct order in EveMarketAnalysisClient/Program.cs

**Checkpoint**: User can log in via EVE SSO and see their character name in the navbar. The full PKCE + token exchange flow works end-to-end.

---

## Phase 4: User Story 2 - View Character Skills Summary (Priority: P2)

**Goal**: Authenticated user navigates to /CharacterSummary and sees portrait, skills grouped by category (Science, Industry, Trade, etc.), and active skill queue.

**Independent Test**: Log in, navigate to /CharacterSummary, verify portrait image, skill groups with levels/SP, and skill queue display.

### Tests for User Story 2

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T031 [P] [US2] Write SkillFilterService unit tests (filters to correct skill groups by name, groups skills by category, calculates SP totals per group, handles empty skill list, handles character with no relevant skills) in EveMarketAnalysisClient.Tests/Unit/SkillFilterServiceTests.cs
- [X] T032 [P] [US2] Write EsiAuthenticationProvider tests (injects Bearer header with valid token, triggers refresh call when token expired, does not inject header when no token present) in EveMarketAnalysisClient.Tests/Services/EsiAuthenticationProviderTests.cs
- [X] T033 [P] [US2] Write CharacterService tests (returns cached CharacterSummary on cache hit, fetches fresh data on cache miss via mocked Kiota ApiClient, aggregates portrait+skills+queue into CharacterSummary record, handles partial ESI failures gracefully with available data) in EveMarketAnalysisClient.Tests/Services/CharacterServiceTests.cs
- [X] T034 [P] [US2] Write CharacterSummary page tests (renders character name and portrait image, renders skill groups with labeled sections, renders skills with name/level/SP within groups, renders skill queue with current training and completion estimate, shows "no skills training" when queue empty, redirects to login when unauthenticated) in EveMarketAnalysisClient.Tests/Pages/CharacterSummaryPageTests.cs

### Implementation for User Story 2

- [X] T035 [US2] Create ISkillFilterService interface in EveMarketAnalysisClient/Services/Interfaces/ISkillFilterService.cs
- [X] T036 [US2] Implement SkillFilterService (pure functions: FilterToRelevantGroups accepts skill list + group mapping returns filtered ImmutableArray, GroupByCategory groups skills into SkillGroupSummary records, relevant group names: Science, Industry, Trade, Resource Processing, Planet Management, Social) in EveMarketAnalysisClient/Services/SkillFilterService.cs
- [X] T037 [US2] Create ICharacterService interface in EveMarketAnalysisClient/Services/Interfaces/ICharacterService.cs
- [X] T038 [US2] Implement EsiAuthenticationProvider (IAuthenticationProvider: reads access token from HttpContext auth properties, checks ExpiresAt, calls EsiTokenService.RefreshAsync if expired, sets Authorization Bearer header) in EveMarketAnalysisClient/Services/EsiAuthenticationProvider.cs
- [X] T039 [US2] Implement CharacterService (GetCharacterSummaryAsync: fetches character portrait via Kiota Characters[id].Portrait, fetches skills via Characters[id].Skills, fetches skill queue via Characters[id].Skillqueue, fetches skill group names via Universe.Groups, uses SkillFilterService to filter+group, caches result in IMemoryCache with 5-min absolute expiry, cache key esi:{charId}:summary) in EveMarketAnalysisClient/Services/CharacterService.cs
- [X] T040 [US2] Register authenticated Kiota ApiClient in DI (HttpClientRequestAdapter with EsiAuthenticationProvider + EsiRateLimitHandler, base URL https://esi.evetech.net) and register anonymous ApiClient for public endpoints in EveMarketAnalysisClient/Program.cs
- [X] T041 [US2] Create CharacterSummary Razor Page with [Authorize] attribute (calls CharacterService.GetCharacterSummaryAsync, renders portrait image, skill groups as labeled sections with skill name/level/SP, skill queue summary with training skill and completion estimate, empty states for no queue) in EveMarketAnalysisClient/Pages/CharacterSummary.cshtml and EveMarketAnalysisClient/Pages/CharacterSummary.cshtml.cs
- [X] T042 [US2] Add CharacterSummary link to navbar (visible only when authenticated) in EveMarketAnalysisClient/Pages/Shared/_Layout.cshtml

**Checkpoint**: Authenticated user can view character summary with portrait, grouped skills, and skill queue. Unauthenticated users are redirected to login.

---

## Phase 5: User Story 3 - Token Lifecycle Management (Priority: P2)

**Goal**: Access tokens refresh transparently when expired. Logout clears all tokens. Session persists across browser restarts.

**Independent Test**: Log in, simulate token expiry, verify subsequent API call succeeds without re-login. Click Logout, verify tokens cleared and navbar shows unauthenticated state.

### Tests for User Story 3

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T043 [P] [US3] Write EsiTokenService.RefreshAsync tests (sends correct refresh request to token_endpoint, updates auth cookie with new tokens on success, redirects to login on refresh failure/revocation, serializes concurrent refresh attempts via SemaphoreSlim) in EveMarketAnalysisClient.Tests/Services/EsiTokenServiceRefreshTests.cs
- [X] T044 [P] [US3] Write Logout page tests (clears authentication cookie via SignOutAsync, redirects to home page, navbar shows unauthenticated state after logout) in EveMarketAnalysisClient.Tests/Pages/LogoutPageTests.cs

### Implementation for User Story 3

- [X] T045 [US3] Add RefreshAsync method to EsiTokenService (posts refresh_token grant to token_endpoint, validates new JWT, updates auth cookie via HttpContext.SignInAsync with new EsiTokenSet, uses SemaphoreSlim to prevent concurrent refresh races) in EveMarketAnalysisClient/Services/EsiTokenService.cs
- [X] T046 [US3] Create Auth/Logout Razor Page (calls HttpContext.SignOutAsync to clear auth cookie, redirects to Index page) in EveMarketAnalysisClient/Pages/Auth/Logout.cshtml and EveMarketAnalysisClient/Pages/Auth/Logout.cshtml.cs
- [X] T047 [US3] Verify cookie persistence settings (IsPersistent=true, expiration aligned with refresh token lifetime) and session restoration on browser restart in EveMarketAnalysisClient/Program.cs cookie authentication config
- [X] T048 [US3] Add error handling for expired/revoked tokens in EsiAuthenticationProvider (catch refresh failure, redirect to login with error message) in EveMarketAnalysisClient/Services/EsiAuthenticationProvider.cs

**Checkpoint**: Token refresh is transparent. Logout works. Session persists across browser restarts. Revoked tokens redirect to re-login.

---

## Phase 6: User Story 4 - Industry and Blueprint Overview (Priority: P3, deferrable)

**Goal**: Character summary page additionally shows count of active industry jobs and owned blueprints.

**Independent Test**: Log in with a character that has industry jobs/blueprints, verify counts display on character summary page.

### Tests for User Story 4

> **NOTE: Write these tests FIRST, ensure they FAIL before implementation**

- [X] T049 [P] [US4] Write CharacterService industry/blueprint tests (fetches industry job count via Kiota Characters[id].Industry.Jobs, fetches blueprint count via Characters[id].Blueprints, caches counts, handles empty results as zero counts, handles API errors gracefully with null counts) in EveMarketAnalysisClient.Tests/Services/CharacterServiceIndustryTests.cs
- [X] T050 [P] [US4] Write CharacterSummary page industry section tests (renders industry job count when present, renders blueprint count when present, renders "none" message when counts are zero, omits section gracefully when counts are null/unavailable) in EveMarketAnalysisClient.Tests/Pages/CharacterSummaryIndustryTests.cs

### Implementation for User Story 4

- [X] T051 [US4] Extend CharacterService.GetCharacterSummaryAsync to fetch industry job count via Characters[id].Industry.Jobs.GetAsync and blueprint count via Characters[id].Blueprints.GetAsync, populate IndustryJobCount and BlueprintCount fields on CharacterSummary record (nullable -- null if fetch fails) in EveMarketAnalysisClient/Services/CharacterService.cs
- [X] T052 [US4] Add industry and blueprint count section to CharacterSummary Razor Page (displays counts when available, shows "None active"/"None owned" for zero, omits section when null) in EveMarketAnalysisClient/Pages/CharacterSummary.cshtml

**Checkpoint**: Character summary shows industry job and blueprint counts. Feature is complete.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Error handling UX, auth error pages, final validation

- [X] T053 [P] Write error handling tests (denied consent shows user-friendly error, network failure shows retry message, invalid state shows security error, insufficient scopes shows re-authorize message) in EveMarketAnalysisClient.Tests/Pages/AuthErrorTests.cs
- [X] T054 [P] Write public endpoint tests verifying that unauthenticated users can access public ESI endpoints (markets, universe) via anonymous ApiClient, and that public pages remain functional regardless of auth state (FR-011, SC-007) in EveMarketAnalysisClient.Tests/Services/PublicEndpointTests.cs
- [X] T055 Implement auth error display in Auth/Callback page (render specific error messages based on error type: denied consent, state mismatch, exchange failure, with retry/re-login links) in EveMarketAnalysisClient/Pages/Auth/Callback.cshtml
- [X] T056 Add OAuth metadata discovery failure handling (disable login button or show error banner when metadata endpoint unreachable, log warning) in EveMarketAnalysisClient/Pages/Shared/_Layout.cshtml and EveMarketAnalysisClient/Services/EsiOAuthMetadataService.cs
- [X] T057 Verify all constitution compliance: immutable records, ImmutableArray collections, pure functions in PkceService/SkillFilterService, no secrets in source/appsettings, TDD red-green-refactor for all tasks
- [X] T058 Run quickstart.md validation (build solution, run all tests, verify app starts and login flow initiates)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies -- can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion -- BLOCKS all user stories
- **US1 Login (Phase 3)**: Depends on Foundational phase completion
- **US2 Character Summary (Phase 4)**: Depends on US1 (needs auth cookie + token)
- **US3 Token Lifecycle (Phase 5)**: Depends on US1 (needs initial token exchange)
- **US4 Industry/Blueprints (Phase 6)**: Depends on US2 (extends CharacterService + summary page)
- **Polish (Phase 7)**: Depends on all user stories being complete

### User Story Dependencies

```text
Phase 1: Setup
    │
    v
Phase 2: Foundational (BLOCKS ALL)
    │
    v
Phase 3: US1 - Login (P1, MVP)
    │
    ├──────────────────┐
    v                  v
Phase 4: US2         Phase 5: US3
Character Summary    Token Lifecycle
(P2)                 (P2)
    │
    v
Phase 6: US4
Industry/Blueprints
(P3, deferrable)
    │
    v
Phase 7: Polish
```

### Within Each User Story

- Tests MUST be written and FAIL before implementation (constitution TDD mandate)
- Models before services
- Services before pages
- DI registration before page usage
- Story complete before moving to next priority

### Parallel Opportunities

- **Phase 1**: T003-T010 (all model records) can run in parallel
- **Phase 2 tests**: T012-T015 can run in parallel
- **Phase 3 tests**: T021-T023 can run in parallel
- **Phase 4 tests**: T031-T034 can run in parallel
- **Phase 4 + Phase 5**: US2 and US3 can run mostly in parallel after US1 completes. **Note**: T038 (EsiAuthenticationProvider, US2) and T048 (US3) both modify `EsiAuthenticationProvider.cs` -- complete T038 before starting T048
- **Phase 5 tests**: T043-T044 can run in parallel
- **Phase 6 tests**: T049-T050 can run in parallel

---

## Parallel Example: User Story 1

```bash
# Launch all tests for US1 together (MUST FAIL first):
Task: T021 "Write EsiTokenService tests"
Task: T022 "Write Login page tests"
Task: T023 "Write Callback page tests"

# Then implement sequentially:
Task: T024 "Create IEsiTokenService interface"
Task: T025 "Implement EsiTokenService"
Task: T026 "Configure cookie auth in Program.cs"
Task: T027 "Create Auth/Login page"
Task: T028 "Create Auth/Callback page"
Task: T029 "Update navbar"
Task: T030 "Register services in DI"
```

## Parallel Example: US2 + US3 after US1

```bash
# These two phases can proceed in parallel after US1:

# Developer A: US2 (Character Summary)
Task: T031-T034 "Write US2 tests in parallel"
Task: T035-T042 "Implement US2 sequentially"

# Developer B: US3 (Token Lifecycle)
# NOTE: T048 modifies EsiAuthenticationProvider.cs -- wait for T038 (US2) first
Task: T043-T044 "Write US3 tests in parallel"
Task: T045-T047 "Implement US3 sequentially (except T048)"
Task: T048 "Add error handling to EsiAuthenticationProvider (after T038 complete)"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL -- blocks all stories)
3. Complete Phase 3: User Story 1
4. **STOP and VALIDATE**: Test full login flow end-to-end
5. Deploy/demo if ready -- user can log in and see their name in navbar

### Incremental Delivery

1. Complete Setup + Foundational -> Foundation ready
2. Add User Story 1 -> Test independently -> Deploy/Demo (MVP!)
3. Add User Story 2 + 3 (parallel) -> Test independently -> Deploy/Demo
4. Add User Story 4 (P3, deferrable) -> Test independently -> Deploy/Demo
5. Polish -> Final validation -> Feature complete

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability
- Constitution mandates TDD: ALL test tasks MUST be completed and failing BEFORE their corresponding implementation tasks
- All data carriers MUST be `record` types with `ImmutableArray` collections
- No secrets in source code -- use `dotnet user-secrets` exclusively
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
