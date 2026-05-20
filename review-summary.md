## Bitwarden Claude Code Review

**Overall Assessment:** REQUEST CHANGES

Reviewed the local working tree for PR #7616 (`PM-33527 Database SQL scripts`). The MSSQL table, stored procedures, migration script, Dapper repository, and entity changes line up cleanly with the rename from `QueuedAt`/`StartedAt`/`LastProgressAt`/`CompletedAt` to the standard `CreationDate`/`StartDate`/`RevisionDate`/`CompletedDate` columns, and the SP renames to `Update*` follow the Bitwarden style guide. One blocking issue remains in the untracked EF Core migration files for MySQL/Postgres/SQLite, plus one substantive question about concurrency semantics of `ReadNextPending` under the documented multi-node Quartz scheduling.

<details>
<summary>Code Review Details</summary>

- ❌ : Untracked EF Core migrations contradict the cloud-only design decision and would create orphan tables in self-hosted MySQL/Postgres/SQLite databases
  - `util/MySqlMigrations/Migrations/20260520184326_AddOrganizationEventCleanup.cs`
  - `util/MySqlMigrations/Migrations/20260520184326_AddOrganizationEventCleanup.Designer.cs`
  - `util/PostgresMigrations/Migrations/20260520183614_AddOrganizationEventCleanup.cs`
  - `util/PostgresMigrations/Migrations/20260520183614_AddOrganizationEventCleanup.Designer.cs`
  - `util/SqliteMigrations/Migrations/20260520183624_AddOrganizationEventCleanup.cs`
  - `util/SqliteMigrations/Migrations/20260520183624_AddOrganizationEventCleanup.Designer.cs`
- ⚠️ : `UPDLOCK, READPAST` on a standalone `SELECT` does not prevent two Quartz nodes from claiming the same row
  - `src/Sql/dbo/Dirt/Stored Procedures/OrganizationEventCleanup_ReadNextPending.sql:6-13`
- ❓ : `ReadNextPending` filters only on `CompletedDate IS NULL`, so rows already claimed by another worker (`StartDate IS NOT NULL`) are re-returned — is that intentional crash recovery, or should it filter `StartDate IS NULL`?
  - `src/Sql/dbo/Dirt/Stored Procedures/OrganizationEventCleanup_ReadNextPending.sql:10-11`

</details>
