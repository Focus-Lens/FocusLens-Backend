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

## Paymob checkout

Checkout defaults to Paymob Egypt sandbox mode. To accept real payments, use an approved merchant account, add the live settings, and set `Paymob__EnableLiveCheckout=true` on the API server. Configure a Paymob integration that completes the charge in one step. The browser and API clients use the same routes in both modes.

Configure secrets with .NET user secrets or server environment variables. In the names below, replace `{Mode}` with `Sandbox` or `Live`.

| Configuration | Value |
| --- | --- |
| `Paymob__{Mode}__SecretKey`, `Paymob__{Mode}__PublicKey`, `Paymob__{Mode}__HmacSecret` | Paymob credentials for this mode |
| `Paymob__{Mode}__CardIntegrationId` | Paymob card integration ID for this mode |
| `Paymob__{Mode}__WalletIntegrationId` | Paymob mobile wallet integration ID for this mode |
| `Paymob__{Mode}__NotificationUrl` | Public HTTPS URL ending in `/api/payments/paymob/webhook/sandbox` or `/api/payments/paymob/webhook/live`, as appropriate |
| `Paymob__{Mode}__ReturnUrl` | HTTPS page that reads `checkoutId` and fetches the payment outcome from the API |

Apply the payment database migrations before enabling checkout. Use an HTTPS tunnel for local Paymob callbacks. Keep the live credentials and callback URL configured after disabling live checkout so outstanding transactions can still finish. The server mode determines which checkouts buyers can start and view. The `isSandbox` response field identifies the mode of each purchase.

- `POST /api/payments/checkouts` creates a hosted checkout. Send a fresh UUID as `checkoutId`, a `plan` of `Plus` or `Family`, a `term` of `Monthly` or `Yearly`, and a `method` of `Card` or `MobileWallet`. Students can omit `studentIds` for a self-purchase. Parents send one linked student ID for Plus or one to three for Family. Supply `billingPhoneNumber` if the buyer account has no usable phone number.
- Reuse `checkoutId` when retrying the same request after a network error. Use a new ID for a new payment attempt. Open the returned `checkoutUrl` when present.
- `GET /api/payments/checkouts/{checkoutId}` returns the order to its buyer. Use it after the redirect or when refreshing payment status. A pending checkout URL is available for up to one hour.
- The Paymob webhook verifies the signature and checks the provider order, integration, amount, and currency. A successful callback records the paid period once. Checkout collects one prepaid term. Automatic renewals and paid-feature enforcement are future work.
