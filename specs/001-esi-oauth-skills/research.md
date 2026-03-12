# Research: ESI OAuth2 Authentication with PKCE + Character Skills Summary

**Branch**: `001-esi-oauth-skills` | **Date**: 2026-03-12

## R1: EVE SSO OAuth Metadata & Endpoint Discovery

**Decision**: Use OAuth Authorization Server Metadata (RFC 8414) at `https://login.eveonline.com/.well-known/oauth-authorization-server`.

**Rationale**: The EVE SSO publishes its configuration at this well-known URL. All endpoint URLs must be fetched dynamically at application startup rather than hard-coded.

**Discovered metadata**:
```json
{
  "issuer": "https://login.eveonline.com",
  "authorization_endpoint": "https://login.eveonline.com/v2/oauth/authorize",
  "token_endpoint": "https://login.eveonline.com/v2/oauth/token",
  "userinfo_endpoint": "https://login.eveonline.com/v2/oauth/verify",
  "jwks_uri": "https://login.eveonline.com/oauth/jwks",
  "revocation_endpoint": "https://login.eveonline.com/v2/oauth/revoke",
  "response_types_supported": ["code", "token"],
  "code_challenge_methods_supported": ["S256"],
  "subject_types_supported": ["public"],
  "token_endpoint_auth_methods_supported": [
    "client_secret_basic", "client_secret_post", "client_secret_jwt"
  ],
  "revocation_endpoint_auth_methods_supported": [
    "client_secret_basic", "client_secret_post", "client_secret_jwt"
  ],
  "id_token_signing_alg_values_supported": ["HS256"],
  "token_endpoint_auth_signing_alg_values_supported": ["HS256"]
}
```

**Key observations**:
- PKCE S256 is confirmed supported.
- No `grant_types_supported` field present; assume `authorization_code` and `refresh_token` based on EVE SSO documentation.
- Token endpoint supports `client_secret_basic`/`post`/`jwt`, but PKCE flow uses no client secret.
- JWKS URI is at `/oauth/jwks` (not versioned), while other endpoints are at `/v2/oauth/`.
- The `userinfo_endpoint` (`/v2/oauth/verify`) can be used to validate tokens and extract character info.

**Alternatives considered**: Hard-coding endpoint URLs. Rejected because spec FR-000 mandates dynamic discovery and EVE may change endpoints.

## R2: Blazor Render Mode for Server-Side Razor Pages App

**Decision**: Keep the existing ASP.NET Core Razor Pages architecture (server-rendered). Do not convert to Blazor WASM or Blazor Server interactive mode.

**Rationale**: The project is currently a standard ASP.NET Core Razor Pages app (`builder.Services.AddRazorPages()` / `app.MapRazorPages()`). Converting to Blazor WASM would be a large scope change. The OAuth flow is naturally server-side: the callback URI is a server endpoint that exchanges the code for tokens. Token storage uses server-side session or `ProtectedLocalStorage` injected via Blazor interactive islands where needed, but the core auth flow runs server-side.

**Approach**: Implement OAuth as server-side middleware/handlers. Token storage uses encrypted HTTP-only cookies via ASP.NET Core Data Protection. The character summary page can use Razor Pages with server-side data fetching. If interactive client-side behavior is needed later, Blazor components can be added incrementally.

**Alternatives considered**:
- Full Blazor WASM: Rejected -- too large a migration, and PKCE token exchange is more secure server-side.
- Blazor Server interactive: Rejected -- unnecessary complexity for this feature; Razor Pages already handles SSR well.
- `ProtectedLocalStorage` (Blazor): Rejected -- requires Blazor interactive mode and exposes tokens to JavaScript-accessible storage.

## R3: PKCE Implementation in .NET 8

**Decision**: Implement PKCE using `System.Security.Cryptography.RandomNumberGenerator` for code verifier generation and `SHA256` for code challenge derivation.

**Rationale**: .NET 8 provides all needed cryptographic primitives. The code verifier must be 43-128 characters from unreserved URI characters (A-Z, a-z, 0-9, `-._~`). The code challenge is `Base64Url(SHA256(code_verifier))`.

**Implementation notes**:
- Generate 32 random bytes → Base64Url encode → yields 43-character verifier.
- Store code verifier + state in server-side temp storage (encrypted cookie or `IDistributedCache`) during the redirect, retrieve on callback.
- State parameter: cryptographically random, stored alongside verifier for CSRF validation.

**Alternatives considered**: Using a library like `IdentityModel`. Rejected -- the PKCE logic is simple enough to implement as pure functions, and adding a dependency for two functions is unnecessary.

## R4: Token Storage Strategy

**Decision**: Store tokens server-side in an encrypted HTTP-only cookie (authentication ticket) using ASP.NET Core's cookie authentication with data protection.

**Rationale**: The spec says "persistent protected storage" surviving browser restarts. Server-side encrypted cookies via ASP.NET Core Data Protection API satisfy this: they're persistent (configurable expiration), protected (encrypted + signed), and survive browser restarts. This is more secure than `ProtectedLocalStorage` (which requires Blazor WASM/Server interactivity). The refresh token is stored in the authentication ticket properties, not exposed to JavaScript.

**Alternatives considered**:
- `ProtectedLocalStorage` (Blazor): Requires Blazor interactive mode; current app is Razor Pages. Also exposes tokens to JavaScript-accessible storage.
- `IDistributedCache` + session: More complex, requires session affinity or external store.
- Cookie selected as simplest secure option for single-user server-rendered app.

## R5: Custom Kiota IAuthenticationProvider

**Decision**: Implement `IAuthenticationProvider` from `Microsoft.Kiota.Abstractions` that reads the access token from the current user's authentication ticket and injects it as a Bearer header.

**Rationale**: Kiota's `ApiClient` accepts an `IRequestAdapter` which takes an `IAuthenticationProvider`. For public (unauthenticated) calls, use `AnonymousAuthenticationProvider`. For authenticated calls, create an `EsiAuthenticationProvider` that:
1. Reads the access token from `HttpContext.User` claims or auth properties.
2. Checks expiration; if expired, uses the refresh token to get a new access token.
3. Updates the stored token in the authentication ticket.
4. Returns the valid access token for the Bearer header.

This keeps the Kiota client wrapper pure (constitution Principle I) -- the auth provider is the boundary where side effects (token refresh HTTP call) occur.

**Alternatives considered**: Manually adding headers to each request. Rejected -- Kiota's `IAuthenticationProvider` is the idiomatic way to handle this.

## R6: JWT Validation Strategy

**Decision**: Validate EVE SSO JWT access tokens using the JWKS endpoint discovered from metadata. Use `System.IdentityModel.Tokens.Jwt` / `Microsoft.IdentityModel.JsonWebTokens` for validation.

**Rationale**: FR-008 requires JWT validation (signature, issuer, audience, expiration). The JWKS URI provides the signing keys. Note: EVE SSO metadata shows `id_token_signing_alg_values_supported: ["HS256"]` -- however, EVE SSO access tokens are RS256-signed JWTs (the JWKS endpoint provides RSA public keys). The HS256 in metadata refers to ID tokens specifically. Validation parameters:
- Issuer: `https://login.eveonline.com`
- Audience: The registered client ID
- Signing keys: From JWKS endpoint (RSA keys)
- Clock skew: 5 minutes (default)

**Alternatives considered**: Skipping local JWT validation and relying on ESI to reject invalid tokens. Rejected -- wastes an API call per request and violates FR-008.

## R7: ESI Scopes Required

**Decision**: Request the following ESI scopes:
- `esi-skills.read_skills.v1` -- character skills
- `esi-skills.read_skillqueue.v1` -- skill queue
- `esi-industry.read_character_jobs.v1` -- industry jobs (P3)
- `esi-characters.read_blueprints.v1` -- blueprints (P3)

**Rationale**: These are the minimum scopes per FR-020. Character public info (name, portrait) does not require a scope. P3 scopes (industry, blueprints) are included in the initial scope request to avoid re-authorization later.

## R8: Skill Group Filtering

**Decision**: Fetch skill group IDs dynamically from the ESI Universe endpoints (`/universe/categories/` and `/universe/groups/`) at startup or first use, then cache. Filter skills to groups whose names match: "Science", "Industry", "Trade", "Resource Processing", "Planet Management", "Social" (contains trade-related skills).

**Rationale**: Hard-coding group IDs is fragile -- CCP could change them. Fetching and caching group metadata from the Universe API aligns with the ESI best practices and the caching constitution constraint. The group names are stable identifiers.

**Alternatives considered**: Hard-coding group IDs. Rejected -- fragile if CCP reorganizes skill categories.

## R9: Caching Strategy

**Decision**: Use `IMemoryCache` with 5-minute expiration for character data (skills, queue, portrait URL, industry jobs, blueprints). Cache keys include character ID for isolation.

**Cache key pattern**: `esi:{characterId}:{dataType}` (e.g., `esi:12345:skills`, `esi:12345:skillqueue`).

**Invalidation**: Time-based only (5-minute absolute expiration). No sliding expiration, no event-based invalidation needed for this feature scope.

**Rationale**: Constitution mandates `IMemoryCache`. Single-user app means no cache size concerns. 5 minutes balances freshness with ESI rate-limit compliance.

## R10: NuGet Packages Required

**Decision**: Add the following packages:

For `EveMarketAnalysisClient`:
- `Microsoft.AspNetCore.Authentication.OpenIdConnect` -- not needed (custom OAuth implementation)
- `System.IdentityModel.Tokens.Jwt` -- JWT validation
- `Microsoft.IdentityModel.Protocols.OpenIdConnect` -- JWKS key retrieval
- `Microsoft.Kiota.Abstractions` -- already in API client project, reference for `IAuthenticationProvider`
- `Microsoft.Kiota.Http.HttpClientLibrary` -- for `HttpClientRequestAdapter`

For `EveMarketAnalysisClient.Tests` (new project):
- `xunit` (v2+)
- `xunit.runner.visualstudio`
- `FluentAssertions`
- `Moq`
- `AutoFixture`
- `AutoFixture.AutoMoq`
- `Microsoft.NET.Test.Sdk`
- `Microsoft.AspNetCore.Mvc.Testing` -- for integration tests

**Rationale**: Constitution mandates the test stack. JWT/OIDC packages are standard Microsoft libraries for token handling. Project reference to `EveStableInfrastructureApiClient` needed for Kiota types.
