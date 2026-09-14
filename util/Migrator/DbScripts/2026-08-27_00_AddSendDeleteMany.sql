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
    -- XACT_ABORT makes this all-or-nothing: DeleteManyAsync needs a throw here to mean nothing was
    -- deleted, so the caller doesn't emit Send_Deleted_* events for rows that don't exist.
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

    -- Returned to the caller so it can recompute storage per id, outside this transaction.
    SELECT DISTINCT
        [UserId]
    FROM
        #Temp
    WHERE
        [UserId] IS NOT NULL
        AND [Type] = 1 -- File

    DROP TABLE #Temp
END
GO
