# FocusLens Backend

FocusLens is a .NET 11 REST API for a student-focus application. It supports student and parent accounts, email verification, JWT authentication, password resets, Google sign-in, student onboarding, and parent-to-student access.

## Architecture

The solution follows Clean Architecture and CQRS:

- `FocusLens.Domain` — entities, business rules, roles, result types, and abstractions.
- `FocusLens.Application` — MediatR commands/queries and FluentValidation.
- `FocusLens.Contracts` — HTTP request and response contracts.
- `FocusLens.Infrastructure` — EF Core/SQL Server, ASP.NET Core Identity, JWT, migrations, and role seeding.
- `FocusLens.Api` — controllers, email/Google adapters, logging, OpenAPI, and hosting.
- `tests` — domain/application unit tests and API integration tests.

## Technology

- .NET 11 / ASP.NET Core
- Entity Framework Core with SQL Server
- ASP.NET Core Identity
- MediatR, FluentValidation, Serilog, Scalar, MailKit, and xUnit
- JWT bearer authentication with refresh-token rotation

## Getting started

### Prerequisites

- .NET 11 SDK
- SQL Server reachable from the API

### Configuration

Use environment variables, user secrets, or an untracked local configuration file. Do **not** commit passwords, connection strings, JWT secrets, or SMTP credentials.

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost,1433;Database=FocusLensDb;User Id=sa;Password=<password>;TrustServerCertificate=True;"
  },
  "Jwt": {
    "Secret": "at-least-32-characters-long-secret",
    "Issuer": "FocusLens.Api",
    "Audience": "FocusLens.Client",
    "TokenExpirationInMinutes": 15,
    "RefreshTokenExpirationInDays": 30
  },
  "Registration": {
    "TermsVersion": "2026-09-04",
    "EmailVerificationCodeLifetimeMinutes": 10
  },
  "GoogleAuth": { "ClientId": "<google-oauth-client-id>" },
  "BehavioralIntelligence": { "BaseUrl": "http://localhost:8000/" },
  "MailSettings": {
    "Mail": "no-reply@example.com",
    "DisplayName": "FocusLens",
    "Password": "<smtp-password>",
    "Host": "smtp.example.com",
    "Port": 587
  }
}
```

For example:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<connection-string>" --project src/FocusLens.Api
dotnet user-secrets set "Jwt:Secret" "<at-least-32-character-secret>" --project src/FocusLens.Api
dotnet user-secrets set "BehavioralIntelligence:BaseUrl" "http://localhost:8000/" --project src/FocusLens.Api
dotnet user-secrets set "AI2_SERVICE_KEY" "<same-service-key-configured-in-Python-AI1>" --project src/FocusLens.Api
```

The Python behavioral-analysis service (AI 1) requires the `X-Service-Key` header. Set `AI2_SERVICE_KEY` to the same value configured in AI 1. Use user secrets for local Development and an environment variable or secret manager for tests and deployments. Do not commit the key. Window-level active time is persisted per behavior window; Reports derives session focus quality from active-time-weighted scores, the dominant state from active-time weights (with AI 1's severity order as a tie-breaker), and the trend from the latest up-to-three non-null window scores (±8 points). Sessions with any historical windows missing the active-time field retain the previous latest-window display rather than mixing weighted and unweighted records. The Parent Overview query remains a separate integration task and should use `SessionFocusMetricsCalculator` after loading all windows for each session.

### Database and run

```bash
dotnet ef database update --project src/FocusLens.Infrastructure --startup-project src/FocusLens.Api
dotnet run --project src/FocusLens.Api
```

In Development, startup applies migrations and seeds the `Parent` and `Student` roles. Use `/scalar` for the interactive API reference and `/health` for a health response.

## Tests

```bash
dotnet test FocusLens.slnx
```

Tests cover refresh-token and relationship domain rules, access handlers, and API health/authorization behavior.

## Deploy to the existing MonsterASP site

The [GitHub Actions workflow](.github/workflows/deploy-monsterasp.yml) builds and tests with the .NET 11 SDK on every push to `main`. If the build and tests pass, it deploys through Web Deploy and checks the live health endpoint. It does not use MonsterASP's Git Deploy builder. Treat pushes to `main` as production releases; the API applies pending database migrations on startup. Use a separate branch for unfinished work. Manual `build-only`, `preview`, and `deploy` runs are still available.

1. In GitHub, open **Actions → Deploy backend to MonsterASP → Run workflow** on `main` with **build-only** to verify the cloud build without credentials.
2. In MonsterASP, open the website's **Deploy → Visual Studio access** and enable **WebDeploy**. In GitHub, under **Settings → Secrets and variables → Actions**, add these repository secrets from that card: `WEBDEPLOY_SITE` (Site), `WEBDEPLOY_URL` (the full HTTPS `msdeploy.axd?site=…` endpoint), `WEBDEPLOY_USERNAME` (Login), and `WEBDEPLOY_PASSWORD`. Do not paste their values into the workflow.
3. Run **preview** to check Web Deploy without applying changes. Before running **deploy**, back up the site and database; the API applies pending database migrations on startup.
4. Run **deploy** to update the live site and check its health endpoint. The sync preserves the existing `web.config`, `appsettings.Production.json`, and `App_Data`, and does not remove extra destination files. This workflow is for the existing site, which already has a working IIS startup configuration.

Live deployments are recorded under GitHub **Deployments → monsterasp-production**. Build-only and preview runs appear only under **Actions**.

## Project structure

```text
src/
  FocusLens.Api/            HTTP API and external adapters
  FocusLens.Application/    CQRS handlers and validation
  FocusLens.Contracts/      API contracts
  FocusLens.Domain/         Business model and abstractions
  FocusLens.Infrastructure/ Persistence, Identity, JWT, migrations
tests/
  FocusLens.Api.IntegrationTests/
  FocusLens.Application.UnitTests/
  FocusLens.Domain.UnitTests/
```
