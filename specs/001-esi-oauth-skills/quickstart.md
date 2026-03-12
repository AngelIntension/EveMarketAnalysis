# Quickstart: ESI OAuth2 + Character Skills Summary

## Prerequisites

1. .NET 8 SDK installed
2. EVE Online developer application registered at https://developers.eveonline.com
   - Callback URL: `https://localhost:7272/Auth/Callback`
   - Scopes: `esi-skills.read_skills.v1 esi-skills.read_skillqueue.v1 esi-industry.read_character_jobs.v1 esi-characters.read_blueprints.v1`

## Setup

```bash
# Clone and checkout feature branch
git checkout 001-esi-oauth-skills

# Configure user secrets (one-time)
cd EveMarketAnalysisClient
dotnet user-secrets set "Esi:ClientId" "YOUR_CLIENT_ID_HERE"
dotnet user-secrets set "Esi:RedirectUri" "https://localhost:7272/Auth/Callback"

# Build solution
cd ..
dotnet build EveMarketAnalysis.sln

# Run tests
dotnet test EveMarketAnalysisClient.Tests/

# Run the app
dotnet run --project EveMarketAnalysisClient
# Open https://localhost:7272
```

## Usage

1. Navigate to https://localhost:7272
2. Click "Login with EVE" in the navbar
3. Authorize on the EVE SSO page
4. View your character summary at /CharacterSummary

## Key Configuration (user-secrets)

| Key | Description |
|-----|-------------|
| `Esi:ClientId` | Your EVE developer app client ID |
| `Esi:RedirectUri` | Must match EVE developer portal registration |
