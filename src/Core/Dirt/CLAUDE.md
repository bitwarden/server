# Access Intelligence, Reports & Event Integrations (Server)

> Scope: `src/Core/Dirt/` and its children. Claude Code reads CLAUDE.md files hierarchically
> (repo root + every parent dir), so this supplements the repo-wide `.claude/CLAUDE.md` with
> domain-specific context. It does NOT auto-load in sibling subtrees such as
> `src/Api/Dirt/`, `src/Sql/dbo/Dirt/`, `src/Infrastructure.*/Dirt/`, `src/Events/`, or
> `src/EventsProcessor/` - see "Where this code lives" for those.

## Project Overview

This subtree holds the server-side domain logic for **Access Intelligence / Reports**, **Event
Integrations**, the **Events audit pipeline**, and **Phishing Detection** support. This is the
.NET (C#) server; it stores and serves data. Unencrypted vault data never reaches the server
(zero-knowledge) - the server persists encrypted blobs and non-secret metadata.

> Owned by `@bitwarden/team-data-insights-and-reporting-dev` (DIRT) via CODEOWNERS.

## Where this code lives

- `src/Core/Dirt/` - domain logic: `Entities/`, `Repositories/` (interfaces), `Reports/` (report
  command/query handlers + file storage), `EventIntegrations/` + `Services/` (Slack/Teams/webhook/
  HEC/Datadog), `Enums/`, `Models/`, `Utilities/`.
- `src/Api/Dirt/` - controllers (`OrganizationReportsController`, `ReportsController`, the
  `OrganizationIntegration*`/`SlackIntegration`/`TeamsIntegration` controllers, `EventsController`,
  `HibpController`) + request/response models + `Public/` API.
- `src/Sql/dbo/Dirt/` - raw SQL: `Tables/`, `Stored Procedures/`, `Views/`.
- `src/Infrastructure.Dapper/Dirt/` - Dapper repositories (production data path).
- `src/Infrastructure.EntityFramework/Dirt/` - EF Core repositories + `Configurations/` + `Models/`.
- `src/Events/` - the Events collector app (ingests audit events). `src/EventsProcessor/` - Azure
  queue processor. Both are DIRT-owned but live outside `src/Core/Dirt/`.

## Architecture & Patterns

- **Data access is a triple-write.** Adding or changing a stored entity touches three places that
  MUST stay in sync:
  1. `src/Sql/dbo/Dirt/` - table, stored procedures, and any view (plus a migration script under
     `util/Migrator/DbScripts/`).
  2. `src/Infrastructure.Dapper/Dirt/` - the Dapper repository (production path).
  3. `src/Infrastructure.EntityFramework/Dirt/` - the EF Core repository, entity `Configurations/`,
     and `Models/` (EF providers + integration tests).
  Reference existing entities that already span all three: `OrganizationReport`,
  `OrganizationApplication`, `Event`.
- **Commands / Queries.** Report operations under `Reports/ReportFeatures/` are one-class-per-operation
  handlers (`*Command` / `*Query`) registered in `ReportingServiceCollectionExtensions`. Follow that
  one-class-per-operation shape; keep controllers thin and put logic in Core. (That extension predates
  the `TryAdd*` convention and still uses `AddScoped` - new registrations should prefer `TryAdd*`.)
- **DI:** use the `TryAdd*` pattern (ADR 0026). No code regions (root rule).
- **Naming:** "Access Intelligence" is the current name; "Risk Insights" is the deprecated name for
  the same feature (legacy paths/classes like `RiskInsightsReportQuery` remain during migration).
  Use "Access Intelligence" in new code, comments, and API surface. V2 report work is gated behind
  `FeatureFlagKeys.AccessIntelligenceNewArchitecture`.

## Feature Flags

DIRT server flags are string constants in `src/Core/Constants.cs` (`FeatureFlagKeys`):
`PhishingDetection`, `AccessIntelligenceNewArchitecture`
(`pm-31936-access-intelligence-new-architecture`), `AccessIntelligenceVersion2`,
`AccessIntelligenceAdoptionUxImprovements`. Gate new endpoints/behavior on the appropriate flag;
default new flags off.

## Common Commands

- `dotnet build` / `dotnet test`
- `dotnet format` - format C# before committing
- `dotnet test test/Core.Test/ --filter "FullyQualifiedName~<Name>"` - run one test
- `pwsh dev/migrate.ps1` - apply local DB migrations after editing `src/Sql/` + adding a migrator script

## Testing Standards

- xUnit. Add/maintain tests for new logic (root rule). DIRT test projects: `test/Core.Test/Dirt`,
  `test/Api.Test/Dirt`, `test/Infrastructure.EFIntegration.Test/Dirt`, and the `Events`/
  `EventsProcessor` test projects.
- Use `NSubstitute` + `AutoFixture` (`[SutAutoData]`/`[BitAutoData]`) following existing DIRT specs.
- Deterministic data only; no PII or real secrets in fixtures.

## Security & Compliance

- **Zero-knowledge:** the server never sees plaintext vault data. Never log or return PII, keys, or
  decrypted data; audit events and report metadata may carry org/user IDs - keep them out of logs.
- New encryption logic does not belong here; follow the security definitions
  (https://contributing.bitwarden.com/architecture/security/definitions).
- Respect CODEOWNERS; AI-config and security-sensitive changes are review-required, never auto-merged.

## Gotchas & Tips

- Forgetting one leg of the triple-write (SQL / Dapper / EF) is the most common DIRT server mistake -
  a Dapper-only change passes some tests but breaks EF-provider/integration paths.
- Report file storage has Azure / Local / Noop implementations (`Reports/Services/`); pick by
  environment, do not hardcode Azure.
- When extending an abstraction, confirm it still serves more than one consumer before adding to it.

## References

- Event Integrations design (deep dive): `src/Core/Dirt/EventIntegrations/README.md`
  (and the scoped `src/Core/Dirt/EventIntegrations/CLAUDE.md`)
- Events pipeline apps context: `src/Events/CLAUDE.md`
- Repo-wide rules and commands: `.claude/CLAUDE.md`
- Contributing Claude context to this repo: `.claude/CONTRIBUTING.md`
- Server architecture: https://contributing.bitwarden.com/architecture/server/
- ADRs: https://contributing.bitwarden.com/architecture/adr/
- Caching (used by Event Integrations): `src/Core/Utilities/CACHING.md`
