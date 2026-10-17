# Bitwarden Server - Claude Code Configuration

## Project Context Files

**Read these files before reviewing to ensure that you fully understand the project and contributing guidelines**

1. `README.md` (read on demand for project overview)
2. `CONTRIBUTING.md` (read on demand for contributing guidelines)
3. @../.github/PULL_REQUEST_TEMPLATE.md

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

## Scripting

New scripts in this repo are written in PowerShell (`.ps1`) — `pwsh` is already the toolchain for `dev/migrate.ps1` and
other developer scripts and runs cross-platform for contributors. Do not add new `.sh`, `.py`, `.rb`, `.js`, or
other-language scripts, and do not add new per-platform pairs (`.sh` + `.ps1`) to accomplish the same task.

**Exceptions**:

- Scripts that execute inside a runtime image or devcontainer lifecycle (for example `entrypoint.sh`, `build.sh`,
  `util/Nginx/*.sh`, `util/MsSql/*.sh`, `.devcontainer/**/*Command.sh`) stay in POSIX shell — the runtime images do not
  ship PowerShell.
- Scripts executed by a JavaScript-only runtime (k6 scenarios under `perf/load/`, Node build tooling and MJML components
  under `src/Core/MailTemplates/Mjml/`) stay in JavaScript — the toolchain will not execute anything else.

This applies to standalone script files only, not inline command steps in other tools' config (Dockerfile `RUN`, GHA
`run:`, `package.json` `"scripts"`). Existing scripts stay as they are, including when modified. The rule governs newly
created files only.

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

## References

- [Server architecture](https://contributing.bitwarden.com/architecture/server/)
- [Architectural Decision Records (ADRs)](https://contributing.bitwarden.com/architecture/adr/)
- [Contributing guidelines](https://contributing.bitwarden.com/contributing/)
- [Setup guide](https://contributing.bitwarden.com/getting-started/server/guide/)
- [Code style](https://contributing.bitwarden.com/contributing/code-style/)
- [Bitwarden security whitepaper](https://bitwarden.com/help/bitwarden-security-white-paper/)
- [Bitwarden security definitions](https://contributing.bitwarden.com/architecture/security/definitions)
