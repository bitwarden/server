# Bitwarden Server - Claude Code Configuration

## Project Context Files

**Read these files before reviewing to ensure that you fully understand the project and contributing guidelines**

1. @README.md
2. @CONTRIBUTING.md
3. @.github/PULL_REQUEST_TEMPLATE.md

## Critical Rules

- **NEVER** use code regions: If complexity suggests regions, refactor for better readability
- **NEVER** compromise zero-knowledge principles: User vault data must remain encrypted and inaccessible to Bitwarden
- **NEVER** log or expose sensitive data: No PII, passwords, keys, or vault data in logs or error messages
- **ALWAYS** use secure communication channels: Enforce confidentiality, integrity, and authenticity
- **ALWAYS** encrypt sensitive data: All vault data must be encrypted at rest, in transit, and in use
- **ALWAYS** prioritize cryptographic integrity and data protection
- **ALWAYS** add unit tests (with mocking) for any new feature development

## Project Structure

- **Source Code**: `/src/` - Services and core infrastructure
- **Tests**: `/test/` - Test logic aligning with the source structure, albeit with a `.Test` suffix
- **Utilities**: `/util/` - Migration tools, seeders, and setup scripts
- **Dev Tools**: `/dev/` - Local development helpers
- **Configuration**: `appsettings.{Environment}.json`, `/dev/secrets.json` for local development

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

## Development Workflow

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

## Migrations

We use two different database providers, Dapper for SQL Server and Entity Framework (EF) for SQLite, MySQL and Postgres. When
a schema change lands it must be reflected in BOTH tracks (Dapper/MSSQL and EF). Skipping either silently breaks one of
the four supported databases.

### Dapper

We maintain an SSDT project at `src/Sql/dbo/` (current state of the database) and hand-written dated migrations
under `util/Migrator/DbScripts/`. Every schema change updates both. Apply locally with `pwsh dev/migrate.ps1`.

See:

- https://contributing.bitwarden.com/contributing/database-migrations/mssql — how to write migrations.
- https://contributing.bitwarden.com/contributing/code-style/sql — SQL code style (file naming, `IF COL_LENGTH`
  guards, `CREATE OR ALTER`, etc.).

### Entity Framework (EF)

EF migrations under `util/{Postgres,MySql,Sqlite}Migrations/` are auto-generated from the EF entity classes in
`src/Infrastructure.EntityFramework/`. Update the entity first, then generate.

**NEVER** hand-write a migration `.cs` file or its `.Designer.cs` snapshot. 
**Always:**

1. Start MySQL — the design-time factory probes the live server for its version:
   ```
   docker compose --profile mysql up -d
   ```
2. Generate migrations for all three providers and refresh each `DatabaseContextModelSnapshot.cs`:
   ```
   pwsh dev/ef_migrate.ps1 <MigrationName>
   ```

To regenerate just one provider: `dotnet ef migrations add <Name> -s util/MySqlMigrations`.

## References

- [Server architecture](https://contributing.bitwarden.com/architecture/server/)
- [Architectural Decision Records (ADRs)](https://contributing.bitwarden.com/architecture/adr/)
- [Contributing guidelines](https://contributing.bitwarden.com/contributing/)
- [Setup guide](https://contributing.bitwarden.com/getting-started/server/guide/)
- [Code style](https://contributing.bitwarden.com/contributing/code-style/)
- [Bitwarden security whitepaper](https://bitwarden.com/help/bitwarden-security-white-paper/)
- [Bitwarden security definitions](https://contributing.bitwarden.com/architecture/security/definitions)
