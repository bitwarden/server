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
    -- XACT_ABORT makes this all-or-nothing: the caller batches the DELETE and the audit-event
    -- loop separately (DeleteManySendsAsync), so it needs a throw here to mean nothing was
    -- deleted — otherwise a failure partway through the cursor would leave the DELETE committed
    -- with no way to emit the Send_Deleted_* events for rows that no longer exist to retry.
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

    COMMIT TRANSACTION Send_DeleteMany

    DROP TABLE #Temp
END
GO
