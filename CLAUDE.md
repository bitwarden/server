# Bitwarden Server - Claude Code Configuration

## Critical Rules

- **NEVER** edit: `/bin/`, `/obj/`, `/.git/`, `/.vs/`, `/packages/`, generated migration files
- **Security First**: All code changes must prioritize cryptographic integrity and data protection
- **Test Coverage**: New features require xUnit unit tests with NSubstitute mocking
- **Check CODEOWNERS requirements**: The repo has a `.github/CODEOWNERS` file to define team ownership for different parts of the codebase. Respect that code owners have final authority over their designated areas

## Project Context

**Architecture**: CQRS pattern with feature-based organization
**Framework**: .NET 8.0, ASP.NET Core
**Database**: SQL Server primary, EF Core supports PostgreSQL, MySQL/MariaDB, SQLite
**Testing**: xUnit 2.4.1, NSubstitute
**Container**: Docker, Docker Compose, Kubernetes/Helm deployable

## Technology Stack

- **Backend Framework**: ASP.NET Core & .NET Core written in C#
- **Services**: Api, Identity, Admin, Notifications, Events, EventsProcessor
- **Auth**: Custom identity system, JWT tokens, PBKDF2-SHA256
- **Encryption**: AES-256-CBC, RSA-2048, zero-knowledge architecture
- **CI/CD**: GitHub Actions - build, test (xUnit), lint (dotnet format), security (Checkmarx)
- **Database**: SQL Server with T-SQL
- **Container Platform**: Docker

## Project Structure

```
/src/
├── Core/               # Business logic, CQRS commands/queries
│   └── OrganizationFeatures/  # Feature-based organization
├── Infrastructure/     # Data access, external services, EF Core
├── Api/               # REST API endpoints
├── Identity/          # Authentication/authorization
└── Sql/               # Database scripts
/test/                 # xUnit test projects
/util/Migrator/        # Database migration tools
```

## Development Standards

### CQRS Pattern

- Commands: `/src/Core/[Feature]/Commands/`
- Queries: `/src/Core/[Feature]/Queries/`
- Handlers implement `ICommandHandler<T>` or `IQueryHandler<T>`

### API Conventions

- RESTful endpoints with standard HTTP status codes
- Consistent error response: `{ "error": { "message": "..." } }`
- Pagination: `?skip=0&take=25`
- API versioning: `/api/v1/`

### Database Migrations

- **SQL Server**: Manual scripts in `/util/Migrator/DbScripts/`
- **Other DBs**: EF Core migrations via `pwsh ef_migrate.ps1`

## Security Requirements

- **Compliance**: SOC 2 Type II, SOC 3, HIPAA, ISO 27001, GDPR, CCPA
- **Principles**: Zero-knowledge, end-to-end encryption, secure defaults
- **Validation**: Input sanitization, parameterized queries, rate limiting
- **Logging**: Structured logs, no PII/sensitive data in logs

## Code Review Checklist

- Security impact assessed
- xUnit tests added/updated
- Performance impact considered
- Error handling implemented
- Breaking changes documented
- CI passes: build, test, lint

## References

- [Architecture](https://contributing.bitwarden.com/architecture/server/)
- [Contributing Guidelines](https://contributing.bitwarden.com/contributing/)
- [Setup Guide](https://contributing.bitwarden.com/getting-started/server/guide/)
- [Code Style](https://contributing.bitwarden.com/contributing/code-style/)
- [Bitwarden security whitepaper](https://bitwarden.com/help/bitwarden-security-white-paper/)
- [Bitwarden security definitions](https://contributing.bitwarden.com/architecture/security/definitions)
