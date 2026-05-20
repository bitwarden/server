# Inline Review Comments — PR #7616

These are the validated findings worth raising. Each one was triaged against the explicit pre-approved decisions in the review prompt (EF Core removal, `IX_Event_OrganizationId` removal, `@Now` parameter passing, SP rename convention, intentional `UPDLOCK, READPAST`) so none of those topics is re-flagged here.

---

## Finding 1 — `util/MySqlMigrations/Migrations/20260520184326_AddOrganizationEventCleanup.cs` (and the two sibling provider migrations)

❌ **CRITICAL**: EF Core migrations for MySQL, Postgres, and SQLite are staged in the working tree even though the EF Core implementation was intentionally removed because this feature is cloud-only.

<details>
<summary>Details and fix</summary>

The review prompt states:

> EF Core implementation was intentionally removed — this feature is cloud-only, self-hosted customers delete the database directly

However, the working tree contains six untracked EF Core migration files that will, if committed, **re-introduce the table into every self-hosted provider** the moment a self-hosted admin runs `dotnet ef database update` or boots a build that auto-applies migrations:

- `util/MySqlMigrations/Migrations/20260520184326_AddOrganizationEventCleanup.cs`
- `util/MySqlMigrations/Migrations/20260520184326_AddOrganizationEventCleanup.Designer.cs`
- `util/PostgresMigrations/Migrations/20260520183614_AddOrganizationEventCleanup.cs`
- `util/PostgresMigrations/Migrations/20260520183614_AddOrganizationEventCleanup.Designer.cs`
- `util/SqliteMigrations/Migrations/20260520183624_AddOrganizationEventCleanup.cs`
- `util/SqliteMigrations/Migrations/20260520183624_AddOrganizationEventCleanup.Designer.cs`

Two confirming signals that these files are stale:

1. The `.Designer.cs` snapshot references `Bit.Infrastructure.EntityFramework.Dirt.Models.OrganizationEventCleanup` — a class that does **not** exist in the source tree. `grep -r "OrganizationEventCleanup" src/Infrastructure.EntityFramework` returns no matches, so the EF model and entity-type-configuration are absent.
2. The provider `DatabaseContextModelSnapshot.cs` files have **not** been updated to include the entity. The next `dotnet ef migrations add` on any of these projects will see the snapshot diverge from the migration and try to generate a corrective migration.

**Fix**: Delete all six files before committing.

```bash
rm util/MySqlMigrations/Migrations/20260520184326_AddOrganizationEventCleanup.cs \
   util/MySqlMigrations/Migrations/20260520184326_AddOrganizationEventCleanup.Designer.cs \
   util/PostgresMigrations/Migrations/20260520183614_AddOrganizationEventCleanup.cs \
   util/PostgresMigrations/Migrations/20260520183614_AddOrganizationEventCleanup.Designer.cs \
   util/SqliteMigrations/Migrations/20260520183624_AddOrganizationEventCleanup.cs \
   util/SqliteMigrations/Migrations/20260520183624_AddOrganizationEventCleanup.Designer.cs
```

If self-hosted should never see this table, the MSSQL-only path in `util/Migrator/DbScripts/` plus the SQL project under `src/Sql/dbo/` is sufficient; no EF migration is needed.

</details>

---

## Finding 2 — `src/Sql/dbo/Dirt/Stored Procedures/OrganizationEventCleanup_ReadNextPending.sql:6-13`

⚠️ **IMPORTANT**: `UPDLOCK, READPAST` on a standalone `SELECT` does not actually serialize claim of a row across multiple Quartz nodes — both nodes can still claim the same row.

<details>
<summary>Details and fix</summary>

The review prompt notes that this SP is called from a Quartz job running on multiple cloud nodes with no `[DisallowConcurrentExecution]`, and that the `UPDLOCK, READPAST` hints are intentional. The hints are necessary but not sufficient: under the default `READ COMMITTED` isolation level with autocommit (no explicit transaction wrapping the call), the `UPDLOCK` is acquired and released **per row as the statement executes**. Once the `SELECT` returns control to Dapper, no lock is held. Two nodes calling `ReadNextPending` back-to-back can each return the same row and then each call `UpdateStarted` — duplicate processing of the same `OrganizationEventCleanup` row.

The standard SQL Server pattern to atomically claim a queue row is:

```sql
CREATE OR ALTER PROCEDURE [dbo].[OrganizationEventCleanup_ReadNextPending]
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE TOP (1) c
    SET
        [StartDate]    = COALESCE([StartDate], @Now),
        [RevisionDate] = @Now
    OUTPUT inserted.*
    FROM
        [dbo].[OrganizationEventCleanup] c WITH (UPDLOCK, READPAST, ROWLOCK)
    WHERE
        [CompletedDate] IS NULL
        AND [StartDate] IS NULL
END
```

This claims and returns the row in a single statement, so the `UPDLOCK` does its job and `UpdateStarted` is no longer needed as a separate round-trip.

Alternatively, if the SELECT/UPDATE separation is preferred for testability, wrap both calls in a `TransactionScope` in C# so the `UPDLOCK` persists across the two SP calls.

If the consumer already provides this guarantee externally (for example, a Quartz `JobStore` lease or a distributed lock), please document that in the SP header — the current code reads as if the hints alone are doing the work.

</details>

---

## Finding 3 — `src/Sql/dbo/Dirt/Stored Procedures/OrganizationEventCleanup_ReadNextPending.sql:10-11`

❓ **QUESTION**: Is it intentional that `ReadNextPending` returns rows that have already been started but never completed?

<details>
<summary>Details and fix</summary>

The `WHERE` clause filters only on `CompletedDate IS NULL`. A row in the "in-progress" state (`StartDate IS NOT NULL`, `CompletedDate IS NULL`) is still eligible to be returned. That could be deliberate — e.g., a worker died mid-job and you want another node to resume — but with no `RevisionDate`-based lease check there is no way to distinguish "in flight on another node right now" from "abandoned by a dead node." Two scenarios to consider:

1. **Intentional resume behavior**: add a lease window, e.g. `AND (StartDate IS NULL OR RevisionDate < DATEADD(MINUTE, -15, @Now))` so healthy in-flight jobs are not double-picked.
2. **No resume desired**: add `AND StartDate IS NULL` so only never-started rows are returned, and rely on `Attempts`/`LastError` plus a separate alert path for crashed jobs.

The first is the more useful behavior for a multi-node Quartz scheduler, and it composes naturally with finding 2's atomic-claim pattern.

</details>
