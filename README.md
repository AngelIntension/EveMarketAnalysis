# Eve Industry Analyzer

A web app for EVE Online players to track character skills, skill queues, industry jobs, and blueprints. Built with ASP.NET Core 8 Razor Pages and the EVE Swagger Interface (ESI).

## Features

- **EVE Online SSO** — OAuth2 PKCE authentication via EVE Online's Single Sign-On
- **Character Skills** — view trained skills grouped by category with skill levels and SP
- **Skill Queue** — see currently training skills with estimated completion times
- **Industry Jobs** — detailed table of active manufacturing, research, copying, invention, and reaction jobs with progress tracking
- **Blueprint Count** — total owned blueprints at a glance
- **EVE Dark Theme** — styled to match the EVE Online aesthetic

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- An [EVE Online developer application](https://developers.eveonline.com/) configured with:
  - Callback URL: `https://localhost:7272/auth/callback`
  - Scopes: `esi-skills.read_skills.v1`, `esi-skills.read_skillqueue.v1`, `esi-industry.read_character_jobs.v1`, `esi-characters.read_blueprints.v1`

## Getting Started

1. Clone the repository:
   ```bash
   git clone https://github.com/AngelIntension/EveMarketAnalysis.git
   cd EveMarketAnalysis
   ```

2. Configure your ESI application credentials in `EveMarketAnalysisClient/appsettings.Development.json` (or user secrets):
   ```json
   {
     "Esi": {
       "ClientId": "your-client-id"
     }
   }
   ```

3. Build and run:
   ```bash
   dotnet build EveMarketAnalysis.sln
   dotnet run --project EveMarketAnalysisClient --launch-profile https
   ```

4. Open https://localhost:7272 in your browser.

## Running Tests

```bash
dotnet test EveMarketAnalysisClient.Tests
```

68 tests covering page handlers, services, and unit logic using xUnit, Moq, and FluentAssertions.

## Project Structure

| Project | Description |
|---|---|
| `EveMarketAnalysisClient/` | ASP.NET Core 8 Razor Pages web app |
| `EveStableInfrastructureApiClient/` | Kiota-generated ESI API client (auto-generated, do not hand-edit) |
| `EveMarketAnalysisClient.Tests/` | xUnit test project |

## Tech Stack

- .NET 8 / ASP.NET Core Razor Pages
- [Microsoft Kiota](https://learn.microsoft.com/en-us/openapi/kiota/) — typed ESI API client
- Bootstrap 5.1 with custom EVE Online dark theme CSS
- xUnit, Moq, FluentAssertions, AutoFixture
