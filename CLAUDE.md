# Bitwarden Server - Claude Code Configuration

## Project Overview

The Bitwarden Server project contains the APIs, database, and other core infrastructure items needed for the "backend" of all bitwarden client applications. The server project is written in C# using .NET Core with ASP.NET Core. The database is written in T-SQL/SQL Server. The codebase can be developed, built, run, and deployed cross-platform on Windows, macOS, and Linux distributions.

This is a security-focused password management backend that handles sensitive user data and cryptographic operations. All code changes must prioritize security, performance, and reliability.

## Technology Stack

- **Backend Framework**: ASP.NET Core (.NET Core)
- **Language**: C#
- **Database**: SQL Server with T-SQL
- **Container Platform**: Docker
- **Architecture**: Distributed Monolith with some API delineation between the various APIs
- **Authentication**: Custom identity system with cryptographic vault operations

## Development Environment Setup

### Prerequisites

- .NET Core SDK (latest LTS version)
- SQL Server or SQL Server Express
- Docker and Docker Compose
- Git with pre-commit hooks enabled

### Environment Commands

```bash
# Set up git pre-commit hooks for automatic formatting
git config --local core.hooksPath .git-hooks

# Docker development environment
docker-compose up -d

# Database migrations (if applicable)
dotnet ef database update

# Run tests
dotnet test

# Build solution
dotnet build

# Run specific project
dotnet run --project src/Api
```

## Project Structure

```
/src/
├── Api/                # Main API endpoints
├── Core/               # Business logic and domain models
├── Infrastructure/     # Data access and external services
├── Identity/           # Authentication and authorization
├── Admin/              # Administrative interfaces
├── Notifications/      # Push notifications and messaging
├── Events/             # Event logging and auditing
└── Sql/                # Database scripts and migrations

/util/                  # Utility scripts and tools
/dev/                   # Development configuration
/scripts/               # Deployment and maintenance scripts
```

## Security Definitions

Apply [Bitwarden security definitions](https://contributing.bitwarden.com/architecture/security/definitions).

## Code Style

Follow [Bitwarden code style standards](https://contributing.bitwarden.com/contributing/code-style/).

## Requirements

- Bitwarden CLI (`bw`) installed and configured
- Node.js 22 with ES modules
- BW_SESSION environment variable

## File Boundaries & Restrictions

### Safe to Edit

- `/src/` - All source code files
- `/test/` - Unit and integration tests
- `/util/` - Utility scripts
- `README.md` and documentation files
- `/dev/` - Development configuration

### Never Edit

- `/bin/`, `/obj/` - Build artifacts
- `/.git/` - Git internal files
- `/.vs/`, `/.vscode/` - IDE configuration
- `/packages/` - NuGet package cache
- Generated migration files (unless specifically instructed)

### Sensitive Areas (Extra Care Required)

- `/src/Core/Models/` - Domain models affecting data structure
- `/src/Infrastructure/EntityFramework/` - Database context and mappings
- `/src/Core/Services/` - Business logic with security implications
- `/src/Identity/` - Authentication and authorization logic
- `/src/Sql/` - Database migration scripts

## Common Commands & Workflows

### Testing

```bash
# Run all tests
dotnet test

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test project
dotnet test test/Api.Test/

# Run integration tests (requires test database)
dotnet test test/IntegrationTest/ --settings test.runsettings
```

### Database Operations

```bash
# Create new migration
dotnet ef migrations add MigrationName --project src/Infrastructure

# Update database
dotnet ef database update --project src/Infrastructure

# Script migrations for production
dotnet ef migrations script --output migration.sql
```

### Docker Operations

```bash
# Build all services
docker-compose build

# Start development environment
docker-compose up -d

# View logs
docker-compose logs -f api

# Clean up containers
docker-compose down --volumes
```

## API Development Guidelines

### REST Conventions

- Use standard HTTP status codes
- Implement consistent error response format
- Use proper HTTP methods (GET, POST, PUT, DELETE)
- Version APIs using URL path: `/api/v1/`
- Implement pagination for collection endpoints

### Authentication & Authorization

- All endpoints require authentication unless explicitly marked as public
- Use JWT tokens with proper expiration
- Implement role-based access control (RBAC)
- Validate organization membership for resource access
- Log all authentication attempts and failures

## Deployment & Production

### Environment Configuration

- Use environment variables for all configuration
- Never commit secrets or connection strings
- Implement proper health check endpoints
- Use structured logging with appropriate log levels
- Configure monitoring and alerting

### Performance Considerations

- Cache frequently accessed data appropriately
- Use database indexing for query optimization
- Implement proper connection pooling
- Monitor memory usage and garbage collection
- Profile API endpoints for performance bottlenecks

## Contributing Guidelines

### Pull Request Process

- Create feature branches from `main`
- Write comprehensive unit tests for new functionality
- Update documentation for API changes
- Ensure all CI checks pass before requesting review
- Include security impact assessment for sensitive changes

### Code Review Checklist

- Security implications thoroughly reviewed
- Performance impact considered
- Error handling implemented properly
- Tests provide adequate coverage
- Documentation updated accordingly
- Breaking changes properly communicated

## Additional Notes

- This is an open-source project with enterprise customers
- Security vulnerabilities should be reported privately
- Please refer to the Server Setup Guide in the Contributing Documentation for build instructions, recommended tooling, code style tips, and lots of other great information to get you started.
- Consider the impact on self-hosted deployments when making infrastructure changes
- Maintain backward compatibility for existing API clients
