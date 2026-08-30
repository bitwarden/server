CREATE OR ALTER PROCEDURE [dbo].[Send_ReadByDeletionDateBefore]
    @DeletionDate DATETIME2(7),
    @BatchSize INT = 2000
AS
BEGIN
    SET NOCOUNT ON

    SELECT TOP (@BatchSize)
        *
    FROM
        [dbo].[SendView]
    WHERE
        [DeletionDate] < @DeletionDate
    ORDER BY
        [DeletionDate] ASC
END
GO

CREATE OR ALTER PROCEDURE [dbo].[Send_DeleteMany]
    @Ids [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON
    -- XACT_ABORT makes the DELETE + bump all-or-nothing: DeleteManySendsAsync needs a throw here
    -- to mean nothing was deleted, so it doesn't emit Send_Deleted_* events for rows that don't
    -- exist. The transaction is committed before the storage-recompute cursor below runs — that
    -- cursor's User_UpdateStorage calls otherwise hold X locks (from the bump, above) on every
    -- affected User row for the whole loop, blocking the account-revision-date read every client
    -- sync poll hits. Storage recompute is idempotent and self-healing, so it doesn't need to
    -- share the DELETE + bump's atomicity — a failure there just leaves that user's Storage stale
    -- until the next Send affecting them is created or deleted.
    SET XACT_ABORT ON

    CREATE TABLE #Temp
    (
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [UserId] UNIQUEIDENTIFIER NULL,
        [Type] TINYINT NOT NULL
    )

    INSERT INTO #Temp
    SELECT
        [Id],
        [UserId],
        [Type]
    FROM
        [dbo].[Send]
    WHERE
        [Id] IN (SELECT [Id] FROM @Ids)

    BEGIN TRANSACTION Send_DeleteMany

    DELETE
    FROM
        [dbo].[Send]
    WHERE
        [Id] IN (SELECT [Id] FROM #Temp)

    DECLARE @UserIds [dbo].[GuidIdArray]
    INSERT INTO @UserIds
    SELECT DISTINCT
        [UserId]
    FROM
        #Temp
    WHERE
        [UserId] IS NOT NULL

    EXEC [dbo].[User_BumpManyAccountRevisionDates] @UserIds

    COMMIT TRANSACTION Send_DeleteMany

    DECLARE @UserId UNIQUEIDENTIFIER
    DECLARE [FileUserCursor] CURSOR FORWARD_ONLY FOR
        SELECT DISTINCT
            [UserId]
        FROM
            #Temp
        WHERE
            [UserId] IS NOT NULL
            AND [Type] = 1 -- File
    OPEN [FileUserCursor]
    FETCH NEXT FROM [FileUserCursor] INTO @UserId
    WHILE @@FETCH_STATUS = 0
    BEGIN
        EXEC [dbo].[User_UpdateStorage] @UserId
        FETCH NEXT FROM [FileUserCursor] INTO @UserId
    END
    CLOSE [FileUserCursor]
    DEALLOCATE [FileUserCursor]

    DROP TABLE #Temp
END
GO
