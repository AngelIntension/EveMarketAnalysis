# Feature Specification: ESI OAuth2 Authentication with PKCE + Basic Character Skills Summary

**Feature Branch**: `001-esi-oauth-skills`
**Created**: 2026-03-12
**Status**: Draft
**Input**: User description: "ESI OAuth2 Authentication with PKCE + Basic Character Skills Summary"

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Login with EVE Online Character (Priority: P1)

A user visits the application and wants to authenticate using their EVE Online character. They click a "Login with EVE" button, which redirects them to the EVE SSO authorization page. After granting consent, they are redirected back to the application and see their character name in the navigation bar, confirming they are logged in.

**Why this priority**: Authentication is the foundational capability that all other personalized features depend on. Without login, no character-specific data can be retrieved.

**Independent Test**: Can be fully tested by completing the login flow and verifying the character name appears in the navbar. Delivers value by establishing the user's identity and enabling all subsequent authenticated interactions.

**Acceptance Scenarios**:

1. **Given** an unauthenticated user on any page, **When** they click "Login with EVE", **Then** they are redirected to the EVE SSO authorization page with a PKCE code challenge (S256 method), correct scopes, and a unique state parameter.
2. **Given** the user has granted consent on the EVE SSO page, **When** they are redirected back to the callback URI, **Then** the application exchanges the authorization code (with code verifier) for an access token and refresh token.
3. **Given** tokens have been obtained, **When** the login flow completes, **Then** the character name is displayed in the navigation bar and the user is redirected to their previous page or the home page.
4. **Given** the user denies consent on the EVE SSO page, **When** they are redirected back, **Then** an informative error message is displayed and they remain unauthenticated.
5. **Given** the state parameter in the callback does not match the original, **When** the callback is processed, **Then** the authentication attempt is rejected and an error is shown.

---

### User Story 2 - View Character Skills Summary (Priority: P2)

An authenticated user navigates to a "Character Summary" page to see an overview of their character's capabilities relevant to industry, trade, and research. They see their character portrait, name, a filtered list of relevant skills with levels and skill points, and their active skill queue.

**Why this priority**: The character summary page is the primary value-add for authenticated users, providing at-a-glance insight into their trade and industry readiness.

**Independent Test**: Can be fully tested by logging in and navigating to the character summary page. Delivers value by showing the user a curated view of their character's skills relevant to market analysis activities.

**Acceptance Scenarios**:

1. **Given** an authenticated user, **When** they navigate to the character summary page, **Then** they see their character name and portrait image.
2. **Given** an authenticated user on the character summary page, **When** the skills data loads, **Then** they see skills grouped by category (Science, Industry, Trade, etc.) with each group displayed as a labeled section, showing each skill's name, trained level, and skill points invested.
3. **Given** an authenticated user with an active skill queue, **When** the page loads, **Then** a summary of the skill queue is displayed showing the skill currently training and estimated completion.
4. **Given** an authenticated user with no active skill queue, **When** the page loads, **Then** a message indicates no skills are currently training.
5. **Given** an unauthenticated user, **When** they attempt to navigate to the character summary page, **Then** they are redirected to the login flow.

---

### User Story 3 - Token Lifecycle Management (Priority: P2)

The application transparently manages the user's session by refreshing expired access tokens and providing a clean logout experience. The user does not need to re-login unless their refresh token is revoked or they explicitly log out.

**Why this priority**: Tied with Story 2 because seamless token management is required for the character summary (and any future authenticated features) to work reliably without constant re-authentication.

**Independent Test**: Can be tested by logging in, waiting for the access token to expire (or simulating expiry), and verifying that subsequent API calls succeed without user intervention. Logout can be tested by clicking "Logout" and verifying tokens are cleared.

**Acceptance Scenarios**:

1. **Given** an authenticated user whose access token has expired, **When** an API call is made, **Then** the application automatically refreshes the access token using the refresh token and retries the request.
2. **Given** an authenticated user whose refresh token has been revoked, **When** an API call fails to refresh, **Then** the user is notified and redirected to re-authenticate.
3. **Given** an authenticated user, **When** they click "Logout", **Then** all stored tokens are cleared, the navbar shows the unauthenticated state, and the user is redirected to the home page.
4. **Given** an authenticated user who closes and reopens the browser, **When** the application loads, **Then** the user remains authenticated (session restored from persistent storage) and the navbar shows their character name.

---

### User Story 4 - Industry and Blueprint Overview (Priority: P3)

An authenticated user can optionally see a count of their active industry jobs and owned blueprints on the character summary page, providing additional context for their production capabilities.

**Why this priority**: This is supplementary information that enriches the character summary but is not essential for the core value proposition of the feature.

**Independent Test**: Can be tested by logging in with a character that has industry jobs and blueprints, then verifying the counts display on the character summary page.

**Acceptance Scenarios**:

1. **Given** an authenticated user with active industry jobs, **When** the character summary page loads, **Then** the number of active industry jobs is displayed.
2. **Given** an authenticated user with owned blueprints, **When** the character summary page loads, **Then** the number of owned blueprints is displayed.
3. **Given** an authenticated user with no industry jobs or blueprints, **When** the character summary page loads, **Then** the industry/blueprints section indicates none are active or owned.

---

### Edge Cases

- What happens when the ESI API is unreachable during the token exchange? The user sees a clear error message and can retry the login.
- What happens when the user's ESI scopes change between sessions (e.g., app requests new scopes in a future version)? The user is prompted to re-authorize with the updated scope set.
- What happens when the character data API returns incomplete data (e.g., portrait endpoint is down)? The page displays available data gracefully with a placeholder for missing elements.
- What happens when the user has thousands of skills? The page only shows skills in relevant groups (Science, Industry, Trade, Resource Processing, Planet Management, Social) to keep the display manageable.
- What happens when tokens are stored but the character has been biomassed or transferred? The API call fails gracefully and the user is prompted to re-authenticate.
- What happens when the browser's cookies are cleared by the user or browser policy? The user is treated as unauthenticated and must log in again.
- What happens when the OAuth metadata discovery endpoint is unreachable? The login button is disabled or shows an error; the application does not attempt authentication with stale or missing endpoint URLs.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-000**: System MUST discover OAuth endpoint URLs (authorization, token, JWKS) dynamically from the EVE SSO metadata document at `https://login.eveonline.com/.well-known/oauth-authorization-server` rather than hard-coding them.
- **FR-001**: System MUST redirect users to the EVE SSO authorization endpoint using the OAuth2 Authorization Code flow with PKCE (S256 code challenge method).
- **FR-002**: System MUST generate a cryptographically random code verifier and derive the code challenge for each authentication attempt.
- **FR-003**: System MUST generate and validate a unique state parameter per authentication attempt to prevent CSRF attacks.
- **FR-004**: System MUST exchange the authorization code and code verifier for access and refresh tokens at the EVE SSO token endpoint.
- **FR-005**: System MUST NOT send a client secret during any part of the authentication flow (PKCE-only).
- **FR-006**: System MUST store tokens securely in encrypted HTTP-only persistent cookies (ASP.NET Core Data Protection), so that the user's session survives browser restarts without requiring re-login.
- **FR-007**: System MUST automatically refresh the access token using the refresh token when the access token expires.
- **FR-008**: System MUST validate JWT access tokens (signature, issuer `https://login.eveonline.com`, audience matching the registered client ID, expiration) before use.
- **FR-009**: System MUST clear all stored tokens and authentication state when the user logs out.
- **FR-010**: System MUST inject a Bearer token into all authenticated ESI API requests via a custom authentication provider.
- **FR-011**: System MUST allow public ESI endpoints (markets, universe) to be called without authentication.
- **FR-012**: System MUST display the authenticated character's name and portrait on the character summary page.
- **FR-013**: System MUST retrieve and display skills filtered to the following skill groups: Science, Industry, Trade, Resource Processing, Planet Management, and Social, organized by category with labeled sections, showing skill name, trained level, and skill points.
- **FR-014**: System MUST retrieve and display the active skill queue summary for the authenticated character.
- **FR-015**: System SHOULD display a count of active industry jobs and owned blueprints for the authenticated character (P3 -- firm requirement but deferrable to a follow-up if P1/P2 stories exceed estimates).
- **FR-016**: System MUST display a navigation bar with login/logout controls and the character name when authenticated.
- **FR-017**: System MUST redirect unauthenticated users to the login flow when they attempt to access protected pages.
- **FR-018**: System MUST handle authentication errors (denied consent, invalid state, token exchange failure, expired/revoked tokens) with user-friendly messages.
- **FR-019**: System MUST cache character data for 5 minutes to reduce redundant API calls to ESI.
- **FR-020**: System MUST request only the minimum ESI scopes needed (skills read, skill queue read, industry jobs read, character blueprints read).

### Key Entities

- **Character**: Represents an EVE Online character. Key attributes: character ID, name, portrait URL. The central identity around which all authenticated data is organized.
- **Skill**: A trained ability belonging to a character. Key attributes: skill ID, skill name, trained level (1-5), skill points invested, skill group. Filtered by relevance to industry/trade/research.
- **Skill Queue Entry**: An item in a character's active training queue. Key attributes: skill ID, skill name, target level, start time, estimated completion time.
- **Token Set**: The authentication credentials for a session. Key attributes: access token, refresh token, expiration time, associated character ID. Managed transparently by the application.
- **Industry Job**: An active manufacturing/research job. Key attributes: job count (summary only for this feature).
- **Blueprint**: A blueprint owned by the character. Key attributes: blueprint count (summary only for this feature).

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Users can complete the full login flow (click "Login with EVE" through seeing their character name in the navbar) in under 10 seconds, excluding time spent on the EVE SSO consent page.
- **SC-002**: The character summary page loads all data (portrait, skills, skill queue) within 3 seconds of navigation for a returning user with cached data.
- **SC-003**: 100% of authentication attempts with valid credentials and granted consent result in a successful login.
- **SC-004**: Token refresh occurs transparently -- users are never asked to re-login due to access token expiration alone (only refresh token revocation or explicit logout).
- **SC-005**: The character summary page correctly displays all relevant skills (Science, Industry, Trade, Resource Processing, Planet Management, and Social groups) for any character, regardless of total skill count.
- **SC-006**: All authentication errors (denied consent, network failure, invalid tokens) produce a user-understandable message within 5 seconds.
- **SC-007**: Public pages (market data, universe info) remain fully functional and performant regardless of the user's authentication state.
- **SC-008**: No sensitive tokens or credentials are ever exposed in application logs, URLs, or browser-accessible source code.

## Clarifications

### Session 2026-03-12

- Q: Should the user's session persist across browser restarts, or end when the browser closes? → A: Session survives browser restarts (persistent storage with refresh token).
- Q: How should skills be organized on the character summary page? → A: Grouped by skill category (Science, Industry, Trade, etc.) with each group as a labeled section.
- Q: How long should character data be cached? → A: 5 minutes.
- Q: Is the industry jobs/blueprints count (FR-015, User Story 4) a hard requirement or droppable? → A: Firm P3 -- included in scope but deferrable to follow-up if P1/P2 exceed estimates.
- Q: How should the application discover ESI OAuth endpoint URLs? → A: Dynamically from `https://login.eveonline.com/.well-known/oauth-authorization-server` (OAuth Authorization Server Metadata, not OpenID Connect discovery). Endpoints MUST NOT be hard-coded.

## Assumptions

- The application is registered as an EVE Online developer application with the appropriate callback URI and scopes configured at the EVE Developer Portal.
- The client ID and redirect URI are configured via dotnet user-secrets and are not committed to source control.
- The EVE SSO endpoints (authorization, token, JWKS) are discovered dynamically via the OAuth Authorization Server Metadata document at `https://login.eveonline.com/.well-known/oauth-authorization-server`. The application MUST NOT hard-code endpoint URLs.
- Skill group categorization (Science, Industry, Trade, Resource Processing, Planet Management, Social) will be determined by matching against well-known EVE Online skill group names. The exact group IDs will be resolved during implementation by querying the Universe endpoints.
- This feature supports a single character per session (multi-character support is out of scope).
- PKCE code verifier and challenge generation is performed server-side using .NET cryptographic primitives (`System.Security.Cryptography`).

## Out of Scope

- Multi-character support (switching between characters in a single session)
- Corporation-level data retrieval and display
- Full profitability calculator or market analysis features
- Advanced UI styling, filtering, or sorting of skills
- Offline mode or service worker caching
- Integration with external tools or APIs beyond EVE ESI
