# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build and Test Commands

```bash
# Build
dotnet build

# Run all tests
dotnet test

# Run a single test project
dotnet test test/Core.Test

# Run a single test class
dotnet test --filter "FullyQualifiedName~DeleteGroupCommandTests"

# Run a single test method
dotnet test --filter "FullyQualifiedName~DeleteGroupCommandTests.DeleteGroup_Success"

# Run locally (API)
dotnet run --project src/Api

# Run locally (Identity)
dotnet run --project src/Identity

# Database migrations
pwsh dev/migrate.ps1              # MSSQL (default)
pwsh dev/migrate.ps1 -postgres    # PostgreSQL
pwsh dev/migrate.ps1 -sqlite      # SQLite
pwsh dev/migrate.ps1 -mysql       # MySQL
pwsh dev/migrate.ps1 -all         # All databases

# Generate OpenAPI specs
pwsh dev/generate_openapi_files.ps1

# Enable git pre-commit hook for formatting
git config --local core.hooksPath .git-hooks
```

## Architecture Overview

### Deployable Services
- **Api** - Main REST API for client applications
- **Identity** - OAuth2/OpenID Connect token server (Duende IdentityServer)
- **Admin** - Administrative portal
- **Billing** - Billing and subscription management
- **Events** - Event logging API
- **EventsProcessor** - Background event processing
- **Notifications** - Real-time push notifications via SignalR
- **Icons** - Domain icon retrieval

### Core Library Structure (`src/Core`)
The Core project is organized by **business domain**, not technical layers:
- `Auth/` - Authentication, SSO, WebAuthn, two-factor
- `Vault/` - Ciphers, folders, collections, sends
- `AdminConsole/` - Organizations, users, policies, groups
- `Billing/` - Subscriptions, payments, licensing
- `SecretsManager/` - Secrets management (commercial)
- `KeyManagement/` - Key rotation, signatures
- `Tools/` - Import/export
- `NotificationCenter/` - User notifications

Cross-cutting concerns:
- `Entities/` - Domain models
- `Repositories/` - Repository interfaces
- `Services/` - Business logic services
- `Context/` - `ICurrentContext` for request-scoped user/org data

### Infrastructure Layer
Two ORM implementations exist for database flexibility:
- **Infrastructure.Dapper** - SQL Server optimized (production cloud)
- **Infrastructure.EntityFramework** - Supports PostgreSQL, MySQL, SQLite (self-hosted)

### Configuration
- `dev/secrets.json` - Local development settings (copy from `secrets.json.example`)
- `appsettings.{Environment}.json` - Environment-specific config
- `GlobalSettings` class - Strongly-typed configuration

## Testing Patterns

See **[test/Common/TESTING.md](../test/Common/TESTING.md)** for detailed testing patterns, examples, and SutProvider usage.

**Quick reference:**
- **Unit tests**: Use `[SutProviderCustomize]`, `[Theory, BitAutoData]`, and `SutProvider<T>` for mocked dependencies
- **Integration tests**: Use `[DatabaseTheory, DatabaseData]` for repository tests, `ApiApplicationFactory` for API tests
- **All tests**: Follow AAA (Arrange-Act-Assert) pattern with clear section comments

## Critical Rules

- **NEVER** compromise zero-knowledge principles: User vault data must remain encrypted and inaccessible to Bitwarden servers
- **NEVER** log sensitive data: No PII, passwords, keys, or vault data in logs or error messages
- **NEVER** use code regions: Refactor for better readability instead
- **ALWAYS** add unit tests with mocking for new features
- **ALWAYS** use nullable reference types (enabled project-wide)

## Key Patterns

- **TryAdd DI pattern** - Services registered via extension methods (ADR 0026)
- **Authorization handlers** - Custom `IAuthorizationHandler` implementations per domain
- **Feature flags** - `IFeatureService` with LaunchDarkly for opt-in features
- **Tokenables** - `DataProtectorTokenFactory` for secure token generation
- **Self-hosted branching** - `if (globalSettings.SelfHosted)` for deployment-specific logic

## References

- [Server architecture](https://contributing.bitwarden.com/architecture/server/)
- [ADRs](https://contributing.bitwarden.com/architecture/adr/)
- [Setup guide](https://contributing.bitwarden.com/getting-started/server/guide/)
- [Code style](https://contributing.bitwarden.com/contributing/code-style/)
