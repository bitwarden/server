# Bitwarden Server - Claude Code Configuration

## Critical Rules

- **NEVER** edit: `/bin/`, `/obj/`, `/.git/`, `/.vs/`, `/packages/` which are generated files
- **NEVER** use code regions: If complexity suggests regions, refactor for better readability
- **NEVER** compromise zero-knowledge principles: User vault data must remain encrypted and inaccessible to Bitwarden
- **NEVER** log or expose sensitive data: No PII, passwords, keys, or vault data in logs or error messages
- **ALWAYS** use secure communication channels: Enforce confidentiality, integrity, and authenticity
- **ALWAYS** encrypt sensitive data: All vault data must be encrypted at rest, in transit, and in use
- **ALWAYS** prioritize cryptographic integrity and data protection
- **ALWAYS** add unit tests (with mocking) for any new feature development

## Project Context

- **Architecture**: Feature and team-based organization
- **Framework**: .NET 8.0, ASP.NET Core
- **Database**: SQL Server primary, EF Core supports PostgreSQL, MySQL/MariaDB, SQLite
- **Testing**: xUnit, NSubstitute
- **Container**: Docker, Docker Compose, Kubernetes/Helm deployable

## Project Structure

- **Source Code**: `/src/` - Api, Identity, Admin, Billing, Core, Infrastructure
- **Tests**: `/test/` - Matching source structure with `.Test` suffix
- **Utilities**: `/util/` - Migration tools, seeders, setup scripts
- **Dev Tools**: `/dev/` - Local development helpers, certificates, docker-compose
- **Configuration**: `appsettings.{Environment}.json`, `/dev/secrets.json` for local dev

## Security Requirements

- **Compliance**: SOC 2 Type II, SOC 3, HIPAA, ISO 27001, GDPR, CCPA
- **Principles**: Zero-knowledge, end-to-end encryption, secure defaults
- **Validation**: Input sanitization, parameterized queries, rate limiting
- **Logging**: Structured logs, no PII/sensitive data in logs

## Common Commands

- **Build**: `dotnet build`
- **Test**: `dotnet test`
- **Run locally**: `dotnet run --project src/Api`
- **Database update**: `pwsh dev/migrate.ps1`
- **Generate OpenAPI**: `pwsh dev/generate_openapi_files.ps1`

## Code Review Checklist

- Security impact assessed
- xUnit tests added / updated
- Performance impact considered
- Error handling implemented
- Breaking changes documented
- CI passes: build, test, lint
- Feature flags considered for new features
- CODEOWNERS file respected

### Key Architectural Decisions

- Use .NET nullable reference types (ADR 0024)
- TryAdd dependency injection pattern (ADR 0026)
- Authorization patterns (ADR 0022)
- OpenTelemetry for observability (ADR 0020)
- Log to standard output (ADR 0021)

## References

- [Server architecture](https://contributing.bitwarden.com/architecture/server/)
- [Architectural Decision Records (ADRs)](https://contributing.bitwarden.com/architecture/adr/)
- [Contributing guidelines](https://contributing.bitwarden.com/contributing/)
- [Setup guide](https://contributing.bitwarden.com/getting-started/server/guide/)
- [Code style](https://contributing.bitwarden.com/contributing/code-style/)
- [Bitwarden security whitepaper](https://bitwarden.com/help/bitwarden-security-white-paper/)
- [Bitwarden security definitions](https://contributing.bitwarden.com/architecture/security/definitions)
