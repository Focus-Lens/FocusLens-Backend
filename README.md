# FocusLens Backend

FocusLens is a .NET 10 REST API for a student-focus application. It supports student and parent accounts, email verification, JWT authentication, password resets, Google sign-in, student onboarding, and parent-to-student access.

## Architecture

The solution follows Clean Architecture and CQRS:

- `FocusLens.Domain` — entities, business rules, roles, result types, and abstractions.
- `FocusLens.Application` — MediatR commands/queries and FluentValidation.
- `FocusLens.Contracts` — HTTP request and response contracts.
- `FocusLens.Infrastructure` — EF Core/SQL Server, ASP.NET Core Identity, JWT, migrations, and role seeding.
- `FocusLens.Api` — controllers, email/Google adapters, logging, OpenAPI, and hosting.
- `tests` — domain/application unit tests and API integration tests.

## Technology

- .NET 10 / ASP.NET Core
- Entity Framework Core with SQL Server
- ASP.NET Core Identity
- MediatR, FluentValidation, Serilog, Scalar, MailKit, and xUnit
- JWT bearer authentication with refresh-token rotation

## Getting started

### Prerequisites

- .NET 10 SDK
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
```

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
