-- Backfill [ActingUserId] on Secrets Manager secret and project events.
--
-- LogUserSecretsEventAsync and LogUserProjectsEventAsync recorded the human actor in [UserId]
-- and left [ActingUserId] NULL. The public /public/events endpoint exposes [ActingUserId] but
-- not [UserId], so these events reported no actor and the actingUserId filter never matched
-- them. New events are now written correctly; this repairs rows written before that change.
--
-- Scope:
--   * Secret_* (2100-2199) and Project_* (2200-2299). Ranges rather than an enum list so that
--     types added in the same release train are not silently skipped forever -- DbUp journals
--     this script and never replays it. The ranges are safe because the two guards below do the
--     real filtering: [ActingUserId] IS NULL excludes every post-fix row, and [UserId] IS NOT
--     NULL excludes machine-account rows, which record [ServiceAccountId] and no user.
--   * ServiceAccount_* (2300-2399) are EXCLUDED, and must stay excluded. They already populate
--     [ActingUserId], and their [UserId] column holds an OrganizationUser id rather than a user
--     id, so copying it would write the wrong class of identifier.
--
-- Re-runnable. Once a row is updated it no longer matches the predicate, and both schema
-- objects are guarded.
--
-- OPERATORS, before running against a large [Event] table:
--   * Run the migrator with --no-transaction. By default DbUp wraps the whole run in a single
--     transaction with a 5 minute per-command timeout (MigratorConstants.DefaultExecutionTimeout-
--     Minutes); --no-transaction raises that to 60 and stops one slow script from rolling back
--     every other migration in the run.
--   * [ActingUserId] is key column 3 of IX_Event_DateOrganizationIdUserId, so each row rewrites
--     that index as well as the base table. Size transaction log accordingly; it is roughly 4x
--     what a single-column update suggests.
--   * Migrations and the server rollout are not atomic, and DbUp journals this script so it
--     never replays. Events written by pre-change code between this script committing and the
--     new server image serving traffic will still have a NULL [ActingUserId]. After the rollout
--     is confirmed, re-run the UPDATE below by hand to sweep that window. The statement is
--     safe to repeat: it is the same predicate and matches nothing once the window is clear.
--
-- Applies to SQL Server only. Self-hosted MySQL, PostgreSQL and SQLite are handled by the
-- matching EF migrations. Bitwarden Cloud keeps events in Azure Table Storage; those rows are
-- not repaired by any database migration.

-- [Event] carries filtered indexes, and SQL Server refuses DML against such a table unless both
-- of these are ON. The .NET client sets them by default; sqlcmd does not (hence its -I switch).
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- ========================================
-- Temporary filtered index
-- ========================================
-- [Event] is the largest table in most deployments and has no index covering this predicate:
-- IX_Event_DateOrganizationIdUserId leads on [Date], and the other two are filtered on
-- OrganizationId/SendId. Without this index every batch would scan the whole table, escalate to
-- a table lock, and block Event_Create for the duration. The filter matches the backfill
-- predicate, so rows drop out of the index as they are updated and each batch stays a seek.
IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID('dbo.Event')
    AND name = 'IX_TEMP_Event_SmEventsMissingActingUser'
)
BEGIN
    PRINT 'Creating temporary filtered index on Event...';
    CREATE INDEX [IX_TEMP_Event_SmEventsMissingActingUser]
        ON [dbo].[Event]([Type])
        WHERE [ActingUserId] IS NULL AND [UserId] IS NOT NULL;
    PRINT 'Temporary index created.';
END
GO

-- ========================================
-- Backfill
-- ========================================
-- Batch size stays under SQL Server's ~5000 lock escalation threshold, and the delay lets the
-- blocked-writer queue on [Event] drain between batches.
DECLARE @BatchSize INT = 2000;
DECLARE @RowsAffected INT = 1;

WHILE @RowsAffected > 0
BEGIN
    UPDATE TOP (@BatchSize) [dbo].[Event]
    SET [ActingUserId] = [UserId]
    WHERE
        ([Type] BETWEEN 2100 AND 2199 OR [Type] BETWEEN 2200 AND 2299)
        AND [ActingUserId] IS NULL
        AND [UserId] IS NOT NULL;

    SET @RowsAffected = @@ROWCOUNT;
    WAITFOR DELAY '00:00:00.100'
END
GO

-- ========================================
-- Drop temporary index
-- ========================================
DROP INDEX IF EXISTS [IX_TEMP_Event_SmEventsMissingActingUser] ON [dbo].[Event];
PRINT 'Temporary index dropped.';
GO
