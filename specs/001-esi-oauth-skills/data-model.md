# Data Model: ESI OAuth2 Authentication + Character Skills Summary

**Branch**: `001-esi-oauth-skills` | **Date**: 2026-03-12

## Entities

All data carriers MUST be `record` types per Constitution Principle I.

### EsiOAuthMetadata

Cached metadata from the OAuth Authorization Server discovery endpoint.

| Field                    | Type                          | Notes                                |
|--------------------------|-------------------------------|--------------------------------------|
| Issuer                   | string                        | Expected: `https://login.eveonline.com` |
| AuthorizationEndpoint    | string (URI)                  | Dynamic from metadata                |
| TokenEndpoint            | string (URI)                  | Dynamic from metadata                |
| JwksUri                  | string (URI)                  | For JWT signature validation         |
| RevocationEndpoint       | string (URI)                  | For token revocation on logout       |
| CodeChallengeMethods     | ImmutableArray\<string\>      | Must contain "S256"                  |

### PkceParameters

Generated per authentication attempt. Ephemeral -- not persisted beyond the auth flow.

| Field          | Type   | Notes                                          |
|----------------|--------|-------------------------------------------------|
| CodeVerifier   | string | 43-128 chars, unreserved URI chars              |
| CodeChallenge  | string | Base64Url(SHA256(CodeVerifier))                 |
| State          | string | Cryptographically random, CSRF protection       |

### EsiTokenSet

Stored in the encrypted authentication cookie. Not directly exposed to browser JS.

| Field            | Type           | Notes                                      |
|------------------|----------------|--------------------------------------------|
| AccessToken      | string         | JWT, validated before use                  |
| RefreshToken     | string         | Used to obtain new access token            |
| ExpiresAt        | DateTimeOffset | Absolute expiration of access token        |
| CharacterId      | int            | Extracted from JWT `sub` claim             |
| CharacterName    | string         | Extracted from JWT `name` claim            |
| Scopes           | ImmutableArray\<string\> | Granted scopes                   |

**State transitions**:
- `None` → `Active` (after successful token exchange)
- `Active` → `Expired` (access token past ExpiresAt)
- `Expired` → `Active` (after successful refresh)
- `Expired` → `None` (refresh token revoked or refresh fails)
- `Active` → `None` (user logs out)

### CharacterSummary

Cached aggregate of character data for the summary page. Immutable snapshot.

| Field           | Type                              | Notes                              |
|-----------------|-----------------------------------|------------------------------------|
| CharacterId     | int                               | From token                         |
| Name            | string                            | From token or /characters/{id}/    |
| PortraitUrl     | string (URI)                      | From /characters/{id}/portrait/    |
| SkillGroups     | ImmutableArray\<SkillGroupSummary\> | Filtered to relevant groups      |
| SkillQueue      | ImmutableArray\<SkillQueueEntry\> | Current training queue             |
| IndustryJobCount| int?                              | P3 -- nullable if not fetched      |
| BlueprintCount  | int?                              | P3 -- nullable if not fetched      |
| FetchedAt       | DateTimeOffset                    | Cache freshness indicator          |

### SkillGroupSummary

A labeled group of skills (e.g., "Industry", "Science").

| Field        | Type                            | Notes                            |
|--------------|---------------------------------|----------------------------------|
| GroupId      | int                             | ESI group ID                     |
| GroupName    | string                          | e.g., "Science", "Trade"        |
| Skills       | ImmutableArray\<CharacterSkill\>| Skills in this group             |
| TotalSp      | long                            | Sum of SP in this group          |

### CharacterSkill

A single trained skill belonging to a character.

| Field            | Type   | Notes                              |
|------------------|--------|------------------------------------|
| SkillId          | int    | ESI type ID                        |
| SkillName        | string | Resolved from Universe types       |
| TrainedLevel     | int    | 0-5                                |
| SkillPointsInSkill | long | SP invested in this skill          |

### SkillQueueEntry

An item in the active training queue.

| Field              | Type            | Notes                              |
|--------------------|-----------------|------------------------------------|
| SkillId            | int             | ESI type ID                        |
| SkillName          | string          | Resolved from Universe types       |
| FinishedLevel      | int             | Target level being trained to      |
| StartDate          | DateTimeOffset? | When training started              |
| FinishDate         | DateTimeOffset? | Estimated completion               |
| QueuePosition      | int             | Position in queue (0-based)        |

## Relationships

```text
EsiTokenSet 1──1 CharacterSummary (via CharacterId)
CharacterSummary 1──* SkillGroupSummary
SkillGroupSummary 1──* CharacterSkill
CharacterSummary 1──* SkillQueueEntry
```

## Validation Rules

- `EsiTokenSet.AccessToken` must be a valid JWT with correct issuer, audience, and non-expired.
- `PkceParameters.CodeVerifier` must be 43-128 characters, containing only `[A-Za-z0-9\-._~]`.
- `PkceParameters.State` must match between authorization request and callback.
- `CharacterSkill.TrainedLevel` must be in range 0-5.
- All collections must be `ImmutableArray<T>` (Constitution Principle I).
