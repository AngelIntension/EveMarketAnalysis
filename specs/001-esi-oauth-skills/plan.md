# Implementation Plan: ESI OAuth2 Authentication with PKCE + Character Skills Summary

**Branch**: `001-esi-oauth-skills` | **Date**: 2026-03-12 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/001-esi-oauth-skills/spec.md`

## Summary

Implement EVE SSO OAuth2 authentication using the PKCE (S256) flow in the existing ASP.NET Core 8 Razor Pages application. After login, display a character summary page showing portrait, filtered industry/trade/research skills grouped by category, active skill queue, and optionally industry job and blueprint counts. All OAuth endpoints discovered dynamically from EVE SSO metadata. Tokens stored server-side in encrypted cookies with transparent refresh. Strict TDD throughout.

## Technical Context

**Language/Version**: C# / .NET 8.0
**Primary Dependencies**: ASP.NET Core 8 Razor Pages, Kiota ESI client (`EveStableInfrastructure.ApiClient`), `System.IdentityModel.Tokens.Jwt`, `Microsoft.IdentityModel.Protocols.OpenIdConnect`
**Storage**: Encrypted HTTP-only cookies (ASP.NET Core Data Protection) for tokens; `IMemoryCache` for ESI data caching
**Testing**: xUnit v2+, FluentAssertions, Moq, AutoFixture (with AutoMoqCustomization)
**Target Platform**: Windows/Linux server, modern browsers
**Project Type**: Web application (ASP.NET Core Razor Pages)
**Performance Goals**: Login flow <10s (excluding SSO consent), character summary page <3s with cache
**Constraints**: PKCE-only (no client secret), 5-minute cache TTL, single character per session
**Scale/Scope**: Single-user personal tool

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Notes |
|-----------|--------|-------|
| **I. Functional & Immutable Style** | PASS | All data carriers are `record` types. Collections use `ImmutableArray`. Pure functions for PKCE generation, skill filtering, JWT parsing. Mutable state only in Razor Page lifecycle (`OnGetAsync`/`OnPostAsync`). |
| **II. Sensitive Data Handling** | PASS | Client ID and redirect URI via `dotnet user-secrets` (UserSecretsId already configured in csproj). Tokens in encrypted cookies, never logged or exposed. No secrets in appsettings or source. |
| **III. Test-Driven Development** | PASS | Mandatory test stack (xUnit, FluentAssertions, Moq, AutoFixture). New `EveMarketAnalysisClient.Tests` project. Tests written before implementation for every component. |
| **Rate-Limit Compliance** | PASS | Respect `X-ESI-Error-Limit-Remain` and `Retry-After` headers. Back-off logic in ESI service wrapper. |
| **Caching** | PASS | `IMemoryCache` with 5-minute expiration. Cache keys include character ID. |
| **Purity of Wrappers** | PASS | ESI service wrappers accept inputs, return immutable records. Side effects (HTTP, caching) at boundary. |
| **Solution Structure** | PASS | Code in `EveMarketAnalysisClient/`. Test project added as `EveMarketAnalysisClient.Tests/`. No changes to Kiota-generated project. |

## Architecture Overview

### Auth Flow Diagram

```text
┌──────────┐     1. Click "Login with EVE"      ┌──────────────────┐
│  Browser  │ ──────────────────────────────────> │  /Auth/Login     │
│           │                                     │  (Razor Page)    │
│           │ <── 2. Redirect (302) ──────────── │  Generates PKCE  │
│           │     to EVE SSO authorize            │  + state, stores │
│           │     ?code_challenge=...             │  in temp cookie  │
│           │     &state=...                      └──────────────────┘
│           │
│           │ ──── 3. User consents on EVE SSO ──>  login.eveonline.com
│           │ <─── 4. Redirect to callback ──────  ?code=...&state=...
│           │
│           │ ──── 5. GET /Auth/Callback ────────> ┌──────────────────┐
│           │                                      │  /Auth/Callback  │
│           │                                      │  Validates state │
│           │                                      │  Exchanges code  │
│           │                                      │  + verifier for  │
│           │                                      │  tokens at EVE   │
│           │                                      │  token endpoint  │
│           │                                      │  Validates JWT   │
│           │ <── 6. Set auth cookie + redirect ── │  Stores tokens   │
│           │     to home or return URL            │  in auth cookie  │
└──────────┘                                      └──────────────────┘

┌──────────┐     7. GET /CharacterSummary         ┌──────────────────┐
│  Browser  │ ──────────────────────────────────> │  CharacterSummary│
│           │                                     │  Razor Page      │
│           │                                     │  Reads token from│
│           │                                     │  auth cookie     │
│           │                                     │  Fetches ESI data│
│           │ <── 8. Rendered HTML ────────────── │  via Kiota client│
└──────────┘                                      └──────────────────┘
```

### Component Architecture

```text
EveMarketAnalysisClient/
├── Program.cs                          # DI registration, auth middleware
├── Services/
│   ├── EsiOAuthMetadataService.cs      # Fetches + caches OAuth metadata
│   ├── PkceService.cs                  # Pure: generates verifier/challenge/state
│   ├── EsiTokenService.cs              # Token exchange, refresh, JWT validation
│   ├── EsiAuthenticationProvider.cs    # Kiota IAuthenticationProvider impl
│   ├── CharacterService.cs            # Fetches character data via Kiota
│   └── SkillFilterService.cs          # Pure: filters/groups skills by category
├── Models/
│   ├── EsiOAuthMetadata.cs             # record
│   ├── PkceParameters.cs               # record
│   ├── EsiTokenSet.cs                  # record
│   ├── CharacterSummary.cs             # record
│   ├── SkillGroupSummary.cs            # record
│   ├── CharacterSkill.cs               # record
│   └── SkillQueueEntry.cs              # record
├── Pages/
│   ├── Auth/
│   │   ├── Login.cshtml(.cs)           # Initiates OAuth flow
│   │   ├── Callback.cshtml(.cs)        # Handles OAuth callback
│   │   └── Logout.cshtml(.cs)          # Clears tokens, redirects
│   ├── CharacterSummary.cshtml(.cs)    # Protected: character data display
│   └── Shared/
│       └── _Layout.cshtml              # Updated: navbar with login/logout
├── Configuration/
│   └── EsiOptions.cs                   # record: ClientId, RedirectUri, Scopes
└── Middleware/
    └── EsiRateLimitHandler.cs          # DelegatingHandler for rate-limit headers
```

### Key Integration Points

**Kiota ApiClient wrapping**: The existing `EveStableInfrastructure.ApiClient` is constructed with an `HttpClientRequestAdapter` that receives either:
- `AnonymousAuthenticationProvider` for public endpoints (markets, universe)
- `EsiAuthenticationProvider` for authenticated endpoints (characters, skills, industry)

Both are registered in DI. `CharacterService` takes the authenticated client via constructor injection.

**Token storage**: ASP.NET Core cookie authentication stores the `EsiTokenSet` as serialized claims + authentication properties in an encrypted, HTTP-only, persistent cookie. The Data Protection API handles encryption. Cookie persistence survives browser restarts (per clarification).

**Token refresh**: `EsiAuthenticationProvider.AuthenticateRequestAsync()` checks `ExpiresAt` before each request. If expired, calls `EsiTokenService.RefreshAsync()` which hits the EVE SSO token endpoint, then updates the cookie via `HttpContext.SignInAsync()` with new token properties.

## Implementation Order & Milestones

### Milestone 1: OAuth Configuration + Discovery + Login Initiation

**Goal**: User can click "Login with EVE" and be redirected to EVE SSO with correct PKCE parameters.

**Components**:
- `EsiOptions` record (ClientId, RedirectUri, Scopes from user-secrets)
- `EsiOAuthMetadataService` (fetches + caches metadata from discovery URL)
- `PkceService` (pure functions: GenerateCodeVerifier, GenerateCodeChallenge, GenerateState)
- `Auth/Login` page (generates PKCE, stores verifier+state in temp cookie, redirects to authorization_endpoint)
- `Program.cs` updates (DI registration, user-secrets configuration)

**Tests** (written first):
- PkceService: verifier length/charset, challenge is correct SHA256+Base64Url, state randomness
- EsiOAuthMetadataService: parses metadata JSON, caches result, handles fetch failure
- EsiOptions: validates required fields

**Depends on**: Nothing (foundation milestone)

---

### Milestone 2: Callback Handling + Token Exchange + Storage

**Goal**: After EVE SSO consent, app exchanges code for tokens and stores them in an encrypted cookie.

**Components**:
- `EsiTokenService` (ExchangeCodeAsync, ValidateJwt, ParseCharacterFromToken)
- `Auth/Callback` page (validates state, calls token exchange, signs in user, redirects)
- Cookie authentication setup in `Program.cs`

**Tests** (written first):
- EsiTokenService: code exchange request format, JWT validation (valid/expired/wrong issuer/wrong audience), character ID extraction from sub claim
- Callback page: state mismatch rejection, successful exchange flow, error handling

**Depends on**: Milestone 1

---

### Milestone 3: Auth State Provider + Protected Routes + Navbar

**Goal**: Navbar shows character name when logged in, login/logout buttons. Protected pages redirect to login.

**Components**:
- `_Layout.cshtml` updates (navbar with conditional auth state)
- Authorization policy for protected pages
- `Auth/Logout` page (clears cookie, redirects)

**Tests** (written first):
- Navbar renders login button when unauthenticated
- Navbar renders character name + logout when authenticated
- Protected page redirects unauthenticated user to login
- Logout clears authentication state

**Depends on**: Milestone 2

---

### Milestone 4: Character Data Fetching (Portrait, Skills, Queue)

**Goal**: Services fetch and cache character data from ESI via Kiota client.

**Components**:
- `EsiAuthenticationProvider` (Kiota `IAuthenticationProvider` -- injects Bearer token)
- `EsiRateLimitHandler` (DelegatingHandler for `X-ESI-Error-Limit-Remain` / `Retry-After`)
- `CharacterService` (GetCharacterSummaryAsync -- orchestrates multiple ESI calls, caches with `IMemoryCache`)
- `SkillFilterService` (pure: filters skills to relevant groups, groups by category, sorts)
- Kiota client registration in DI (authenticated + anonymous instances)

**Tests** (written first):
- EsiAuthenticationProvider: injects token, triggers refresh when expired, skips for anonymous
- EsiRateLimitHandler: backs off when error limit low, respects Retry-After
- CharacterService: returns cached data on cache hit, fetches fresh on miss, handles ESI errors
- SkillFilterService: filters to correct groups, groups skills by category, calculates SP totals, handles empty skills

**Depends on**: Milestone 2 (needs valid token)

---

### Milestone 5: Character Summary UI + Skill Display

**Goal**: Protected `/CharacterSummary` page displays character portrait, skills by category, and skill queue.

**Components**:
- `CharacterSummary.cshtml` + code-behind (calls `CharacterService`, renders data)
- Skill group sections (labeled groups with skill name, level, SP)
- Skill queue summary section
- Loading/error states

**Tests** (written first):
- Page renders character name and portrait
- Skills displayed grouped by category with correct data
- Skill queue shows current training with completion estimate
- Empty queue shows appropriate message
- Error state renders user-friendly message

**Depends on**: Milestones 3, 4

---

### Milestone 6: Token Refresh + Error Handling + P3 Industry/Blueprints

**Goal**: Transparent token refresh, comprehensive error handling, optional industry data.

**Components**:
- `EsiTokenService.RefreshAsync()` (refresh token flow, cookie update)
- Error pages/messages for auth failures
- Industry job count + blueprint count on character summary (P3, deferrable)

**Tests** (written first):
- Token refresh: successful refresh updates cookie, failed refresh redirects to login
- Concurrent refresh requests don't race (single refresh at a time)
- Error scenarios: denied consent, network failure, revoked token, insufficient scopes
- Industry/blueprint counts display correctly (P3)

**Depends on**: Milestone 5

## Key Technical Decisions

### 1. Razor Pages (Not Blazor WASM)

The existing app is ASP.NET Core Razor Pages. The OAuth flow is naturally server-side (code exchange happens at a server endpoint). Converting to Blazor WASM would require a large migration and would expose the token exchange to the browser. Keep Razor Pages; add Blazor components incrementally only if needed.

### 2. NuGet Packages

**EveMarketAnalysisClient** (add):
- `System.IdentityModel.Tokens.Jwt` -- JWT validation
- `Microsoft.IdentityModel.Protocols.OpenIdConnect` -- JWKS key retrieval
- `System.Collections.Immutable` -- `ImmutableArray`, `FrozenDictionary`
- `Microsoft.Kiota.Abstractions` -- `IAuthenticationProvider` interface
- `Microsoft.Kiota.Http.HttpClientLibrary` -- `HttpClientRequestAdapter`
- Project reference: `EveStableInfrastructureApiClient`

**EveMarketAnalysisClient.Tests** (new project):
- `Microsoft.NET.Test.Sdk`
- `xunit` (v2+)
- `xunit.runner.visualstudio`
- `FluentAssertions`
- `Moq`
- `AutoFixture`
- `AutoFixture.AutoMoq`
- `Microsoft.AspNetCore.Mvc.Testing`

### 3. Custom Kiota IAuthenticationProvider

```text
EsiAuthenticationProvider : IAuthenticationProvider
  ├── AuthenticateRequestAsync(RequestInformation)
  │   ├── Read access token from HttpContext auth properties
  │   ├── If expired → call EsiTokenService.RefreshAsync()
  │   └── Set Authorization: Bearer {accessToken} on request
  └── Dependencies: IHttpContextAccessor, EsiTokenService
```

For public endpoints, use Kiota's built-in `AnonymousAuthenticationProvider`. Register two named `ApiClient` instances in DI: `"authenticated"` and `"anonymous"`.

### 4. Skill Group Filtering

Fetch skill categories and groups from ESI Universe endpoints at first use:
1. `GET /universe/categories/{categoryId}` for "Skill" category → get group IDs
2. `GET /universe/groups/{groupId}` for each group → get group names
3. Filter to: "Science", "Industry", "Trade", "Resource Processing", "Planet Management", "Social"
4. Cache the group ID → name mapping (long-lived, skill categories rarely change)

At skill display time: filter character skills to those whose type belongs to a relevant group.

### 5. Caching Strategy

| Cache Key Pattern | TTL | Data |
|---|---|---|
| `esi:metadata` | 24 hours | OAuth metadata document |
| `esi:jwks` | 24 hours | JWKS signing keys |
| `esi:{charId}:skills` | 5 min | Character skills |
| `esi:{charId}:skillqueue` | 5 min | Skill queue |
| `esi:{charId}:portrait` | 5 min | Portrait URL |
| `esi:{charId}:industry` | 5 min | Industry job count |
| `esi:{charId}:blueprints` | 5 min | Blueprint count |
| `esi:skillgroups` | 24 hours | Skill group ID → name mapping |

All via `IMemoryCache` with absolute expiration. No sliding expiration (simpler, predictable).

## Test Plan Outline

### Unit Tests (pure functions, no mocks needed)

- **PkceServiceTests**: verifier format (length, charset), challenge derivation (known test vectors), state uniqueness
- **SkillFilterServiceTests**: filters correct groups, handles empty input, groups by category, calculates SP totals, sorts within groups
- **EsiTokenSet record tests**: immutability, `with` expressions, IsExpired computed property
- **EsiOptions validation**: missing ClientId, missing RedirectUri, empty scopes

### Service Tests (mocked dependencies)

- **EsiOAuthMetadataServiceTests**: parses real metadata JSON (mock HttpClient), caches after first fetch, throws on unreachable endpoint
- **EsiTokenServiceTests**: builds correct token exchange request (mock HttpClient), validates JWT (mock JWKS), handles refresh flow, handles error responses
- **EsiAuthenticationProviderTests**: injects Bearer header (mock HttpContext), triggers refresh for expired token, does not inject header for anonymous requests
- **CharacterServiceTests**: fetches + aggregates character data (mock Kiota ApiClient), returns cached data on hit, handles partial ESI failures gracefully
- **EsiRateLimitHandlerTests**: passes through when error limit high, delays when limit low, respects Retry-After header

### Page/Integration Tests

- **Login page**: generates redirect URL with correct parameters (PKCE, state, scopes)
- **Callback page**: exchanges code, sets cookie, redirects; rejects bad state
- **Logout page**: clears cookie, returns unauthenticated
- **CharacterSummary page**: renders all sections with mock data; redirects when unauthenticated
- **Navbar**: conditional rendering based on auth state

## Risks & Mitigations

| Risk | Impact | Likelihood | Mitigation |
|------|--------|------------|------------|
| ESI rate limits (403 on skills endpoints) | Character data unavailable | Medium | `EsiRateLimitHandler` respects `X-ESI-Error-Limit-Remain`, implements exponential back-off. Cache reduces call frequency. |
| PKCE verifier/challenge encoding mismatch | Login always fails | Low | Unit tests with known test vectors (RFC 7636 appendix B). Base64Url encoding (no padding, `-` and `_` instead of `+` and `/`). |
| Browser clears cookies (privacy mode, user action) | Session lost | Low | Acceptable -- user re-logs in. Edge case documented in spec. |
| EVE SSO metadata endpoint unavailable | Cannot initiate login | Low | Cache metadata for 24h. If cached metadata exists, use it. If no cache, show error on login button. |
| JWT validation fails due to key rotation | Login fails post-rotation | Low | `Microsoft.IdentityModel` auto-refreshes JWKS keys. Cache JWKS for 24h but allow forced refresh on validation failure. |
| Concurrent token refresh race condition | Duplicate refresh requests, potential token invalidation | Medium | Use `SemaphoreSlim` to serialize refresh attempts per character. |

## Remaining Questions / Decisions Needed

1. **Exact redirect URI**: Must match what's registered at the EVE Developer Portal. Assumed `https://localhost:7272/Auth/Callback` for development. User must register this URI at https://developers.eveonline.com.
2. **Portrait image size**: ESI provides 64, 128, 256, 512px. Recommend 128px for summary page. Can be decided during UI implementation.
3. **Skill sorting within groups**: Alphabetical by name, or by level descending? Recommend alphabetical for consistency. Can be adjusted in `SkillFilterService`.

## Project Structure

### Documentation (this feature)

```text
specs/001-esi-oauth-skills/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md        # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/           # Phase 1 output (N/A -- no external API exposed)
├── checklists/
│   └── requirements.md  # Spec quality checklist
└── tasks.md             # Phase 2 output (/speckit.tasks command)
```

### Source Code (repository root)

```text
EveMarketAnalysisClient/
├── Program.cs                          # Updated: DI, auth, user-secrets
├── EveMarketAnalysisClient.csproj      # Updated: new package refs + project ref
├── Configuration/
│   └── EsiOptions.cs
├── Models/
│   ├── EsiOAuthMetadata.cs
│   ├── PkceParameters.cs
│   ├── EsiTokenSet.cs
│   ├── CharacterSummary.cs
│   ├── SkillGroupSummary.cs
│   ├── CharacterSkill.cs
│   └── SkillQueueEntry.cs
├── Services/
│   ├── EsiOAuthMetadataService.cs
│   ├── PkceService.cs
│   ├── EsiTokenService.cs
│   ├── EsiAuthenticationProvider.cs
│   ├── CharacterService.cs
│   ├── SkillFilterService.cs
│   └── Interfaces/
│       ├── IEsiOAuthMetadataService.cs
│       ├── IEsiTokenService.cs
│       ├── ICharacterService.cs
│       └── ISkillFilterService.cs
├── Middleware/
│   └── EsiRateLimitHandler.cs
├── Pages/
│   ├── Auth/
│   │   ├── Login.cshtml + Login.cshtml.cs
│   │   ├── Callback.cshtml + Callback.cshtml.cs
│   │   └── Logout.cshtml + Logout.cshtml.cs
│   ├── CharacterSummary.cshtml + CharacterSummary.cshtml.cs
│   └── Shared/
│       └── _Layout.cshtml              # Updated: auth-aware navbar
└── wwwroot/                            # Existing static files

EveMarketAnalysisClient.Tests/
├── EveMarketAnalysisClient.Tests.csproj
├── Unit/
│   ├── PkceServiceTests.cs
│   ├── SkillFilterServiceTests.cs
│   └── EsiTokenSetTests.cs
├── Services/
│   ├── EsiOAuthMetadataServiceTests.cs
│   ├── EsiTokenServiceTests.cs
│   ├── EsiAuthenticationProviderTests.cs
│   ├── CharacterServiceTests.cs
│   └── EsiRateLimitHandlerTests.cs
└── Pages/
    ├── LoginPageTests.cs
    ├── CallbackPageTests.cs
    └── CharacterSummaryPageTests.cs

EveStableInfrastructureApiClient/       # Unchanged (Kiota-generated)
```

**Structure Decision**: Extend the existing `EveMarketAnalysisClient/` Razor Pages project with new folders for Models, Services, Configuration, and Middleware. Add a separate `EveMarketAnalysisClient.Tests/` xUnit project to the solution. No changes to the Kiota-generated project.

## Complexity Tracking

No constitution violations to justify. All design decisions comply with Principles I-III and infrastructure constraints.
