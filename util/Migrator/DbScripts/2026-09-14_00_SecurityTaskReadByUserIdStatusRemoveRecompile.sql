-- DBOPS-231: remove OPTION (RECOMPILE) from SecurityTask_ReadByUserIdStatus, and give the procedure
-- a covering index to read from.
--
-- The hint was added in PM-21044 (2025-09-12) because the previous body's cost depended on the
-- caller's organization size, so a single cached plan could not serve every caller. Compiling on
-- every execution is expensive in CPU, and each compile also takes schema-stability locks across
-- the referential-integrity closure of the tables in the query, which puts this procedure in the
-- way of schema changes on those tables.
--
-- The new body removes the reason the plan had to vary rather than just dropping the hint: every
-- step is driven by a small staged set, and the set that decides the plan shape is held in a #temp
-- table so the optimizer costs it from real statistics rather than from whichever caller compiled
-- first (see the comments on steps 2 and 3b). Parameters, the projected columns and their order,
-- and the result ordering are unchanged; result equivalence against the previous body was verified
-- on a restore of production.
--
-- The index rebuild is independent of that and simply removes a lookup per row: the procedure reads
-- tasks by organization and returns all seven columns. It is a DROP_EXISTING rebuild of an index
-- that already exists on a table of roughly 17,600 rows, so it is not a large index build. The
-- previous filter was redundant on a NOT NULL column and the EF model never declared it.

-- Rebuild first, so the procedure below compiles against the covering index.
--
-- Two branches because DROP_EXISTING is not a no-op when the index is missing: it fails outright with
-- Msg 7999, "Could not find any index named ...". In practice the index has existed since
-- 2024-11-21_00_SecurityTaskReadByUserIdStatus.sql, so the ELSE branch is defence-in-depth for a
-- database where it was dropped by hand. Both branches must produce the same definition -- keep them
-- in sync if this is ever edited. Deliberately not DROP INDEX + CREATE INDEX, which would leave a
-- window with no index on the table.
IF EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [object_id] = OBJECT_ID('[dbo].[SecurityTask]')
        AND [name] = 'IX_SecurityTask_OrganizationId'
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_SecurityTask_OrganizationId]
        ON [dbo].[SecurityTask] ([OrganizationId] ASC)
        INCLUDE ([Status], [CipherId], [Type], [CreationDate], [RevisionDate])
        WITH (DROP_EXISTING = ON);
END
ELSE
BEGIN
    CREATE NONCLUSTERED INDEX [IX_SecurityTask_OrganizationId]
        ON [dbo].[SecurityTask] ([OrganizationId] ASC)
        INCLUDE ([Status], [CipherId], [Type], [CreationDate], [RevisionDate]);
END
GO

CREATE OR ALTER PROCEDURE [dbo].[SecurityTask_ReadByUserIdStatus]
    @UserId [UNIQUEIDENTIFIER],   -- who is asking
    @Status [TINYINT] = NULL      -- 0 = Pending, 1 = Completed, NULL = don't filter
AS
BEGIN
    SET NOCOUNT ON;

    -- ------------------------------------------------------------------------------------------
    -- STEP 1. Which organizations can this user see tasks in?
    --
    -- Confirmed membership only (Status = 2), and only organizations that are enabled. We keep
    -- both the organization id and the user's membership-row id, because collection grants hang
    -- off the membership row, not off the user.
    --
    -- This is a handful of rows for anybody. That is the whole point: every step below is driven
    -- by this small list, so the work does not scale with how large the user's organizations are.
    -- That is what lets this procedure run without OPTION (RECOMPILE).
    -- ------------------------------------------------------------------------------------------
    DECLARE @Memberships TABLE (
        [OrganizationUserId] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
        [OrganizationId] UNIQUEIDENTIFIER NOT NULL
    );

    INSERT INTO @Memberships ([OrganizationUserId], [OrganizationId])
    SELECT
        [OU].[Id],
        [OU].[OrganizationId]
    FROM
        [dbo].[OrganizationUser] [OU]
    INNER JOIN
        [dbo].[Organization] [O] ON [O].[Id] = [OU].[OrganizationId]
    WHERE
        [OU].[UserId] = @UserId
        AND [OU].[Status] = 2 -- Confirmed
        AND [O].[Enabled] = 1;

    -- ------------------------------------------------------------------------------------------
    -- STEP 2. Which collections is this user allowed to EDIT?
    --
    -- Only worth working out if any of the user's organizations has a task at all -- for a caller
    -- whose orgs have no tasks, the answer cannot matter, so we skip the whole block.
    --
    -- Two routes to an editable collection, and either is sufficient:
    --   a) granted directly to the user      (CollectionUser)
    --   b) granted to a group they belong to (GroupUser -> CollectionGroup)
    -- ReadOnly = 0 is the "can edit" flag on both.
    --
    -- UNION (not UNION ALL) is load-bearing: if both routes grant the same collection it appears
    -- twice, and the primary key below would reject the duplicate.
    --
    -- Why a #temp table and not a table variable
    -- ------------------------------------------
    -- This set drives the access probe in step 3b, and how many rows are in it decides which of
    -- two very different plans the optimizer picks (see the comment there). A table variable has
    -- no statistics: under deferred compilation SQL Server takes the FIRST caller's row count and
    -- bakes it into the cached plan for everyone. A caller with one collection would therefore pin
    -- a plan that costs a caller with 173 collections roughly 128k logical reads instead of 28k --
    -- worse, for that caller, than the OPTION (RECOMPILE) body this replaces. A #temp table
    -- carries real statistics, so each caller's plan reflects the set actually in front of it.
    --
    -- That does mean occasional statement recompiles when the row count moves, which is the thing
    -- this change exists to avoid -- so it was measured rather than assumed. Rotating all 13 test
    -- shapes six times: 8 recompiles in 78 calls, and 16.3 ms CPU per call, against the previous
    -- body's one recompile per call and 60.7 ms. The table is declared with an unnamed inline
    -- primary key so SQL Server can cache it between executions; naming that constraint would
    -- disable the cache and add tempdb allocation to every call.
    -- ------------------------------------------------------------------------------------------
    CREATE TABLE #Cols (
        [CollectionId] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY
    );

    IF EXISTS (
        -- Reading the base table rather than SecurityTaskView is intentional and equivalent:
        -- the view is "SELECT * FROM dbo.SecurityTask" with no filter, and we only test existence.
        SELECT 1
        FROM
            [dbo].[SecurityTask] [ST]
        INNER JOIN
            @Memberships [M] ON [M].[OrganizationId] = [ST].[OrganizationId]
    )
    BEGIN
        INSERT INTO #Cols ([CollectionId])
        SELECT
            [CU].[CollectionId]
        FROM
            [dbo].[CollectionUser] [CU]
        INNER JOIN
            @Memberships [M] ON [M].[OrganizationUserId] = [CU].[OrganizationUserId]
        WHERE
            [CU].[ReadOnly] = 0
        UNION
        SELECT
            [CG].[CollectionId]
        FROM
            [dbo].[GroupUser] [GU]
        INNER JOIN
            @Memberships [M] ON [M].[OrganizationUserId] = [GU].[OrganizationUserId]
        INNER JOIN
            [dbo].[CollectionGroup] [CG] ON [CG].[GroupId] = [GU].[GroupId]
        WHERE
            [CG].[ReadOnly] = 0;
    END

    -- ------------------------------------------------------------------------------------------
    -- STEP 3a. The user can edit nothing (no grants, or their orgs have no tasks).
    --
    -- Then the only tasks they could possibly see are tasks with no cipher attached, because the
    -- "is this cipher in one of my collections" check can never succeed. Answer that directly and
    -- stop. In production every task currently has a cipher, so in practice this returns nothing.
    --
    -- This is not just a shortcut. Giving these callers their own statement keeps them from
    -- compiling the query in step 3b against an empty collection set, which would cache a plan
    -- built for a caller who can see nothing and hand it to everyone else.
    -- ------------------------------------------------------------------------------------------
    IF NOT EXISTS (SELECT 1 FROM #Cols)
    BEGIN
        SELECT
            [ST].[Id],
            [ST].[OrganizationId],
            [ST].[CipherId],
            [ST].[Type],
            [ST].[Status],
            [ST].[CreationDate],
            [ST].[RevisionDate]
        FROM
            [dbo].[SecurityTaskView] [ST]
        INNER JOIN
            @Memberships [M] ON [M].[OrganizationId] = [ST].[OrganizationId]
        WHERE
            (@Status IS NULL OR [ST].[Status] = @Status)
            AND [ST].[CipherId] IS NULL
        ORDER BY
            [ST].[CreationDate] DESC;
        RETURN;
    END

    -- ------------------------------------------------------------------------------------------
    -- STEP 3b. The normal path: the user can edit at least one collection.
    --
    -- Keep a task if it has no cipher, or if its cipher appears in one of the user's editable
    -- collections. EXISTS stops at the first match. Newest first, as the API has always returned.
    --
    -- The access probe has two possible shapes, and they differ by orders of magnitude:
    --
    --   good: for each task, seek CollectionCipher by CipherId (a cipher belongs to very few
    --         collections), then probe the collection set by its primary key.  ~1 seek per task.
    --   bad:  scan the collection set and, for every collection the user can edit, seek
    --         CollectionCipher. A user with 173 editable collections pays 173 seeks per task.
    --
    -- Which one the optimizer picks depends on how many rows it believes are in #Cols, which is
    -- why step 2 uses a table with statistics. Measured across six different compile orders on a
    -- production restore, the totals stayed within 0.09% and the heaviest caller within 28.4k-28.5k
    -- logical reads; with a table variable the same test swung from 133k to 279k.
    -- ------------------------------------------------------------------------------------------
    SELECT
        [ST].[Id],
        [ST].[OrganizationId],
        [ST].[CipherId],
        [ST].[Type],
        [ST].[Status],
        [ST].[CreationDate],
        [ST].[RevisionDate]
    FROM
        [dbo].[SecurityTaskView] [ST]
    INNER JOIN
        @Memberships [M] ON [M].[OrganizationId] = [ST].[OrganizationId]
    WHERE
        (@Status IS NULL OR [ST].[Status] = @Status)
        AND (
            [ST].[CipherId] IS NULL
            OR EXISTS (
                SELECT 1
                FROM
                    [dbo].[CollectionCipher] [CC]
                INNER JOIN
                    #Cols [C] ON [C].[CollectionId] = [CC].[CollectionId]
                WHERE
                    [CC].[CipherId] = [ST].[CipherId]
            )
        )
    ORDER BY
        [ST].[CreationDate] DESC;
END
GO
