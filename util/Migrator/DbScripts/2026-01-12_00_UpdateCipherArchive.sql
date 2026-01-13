CREATE OR ALTER PROCEDURE [dbo].[Cipher_Unarchive]
    @Ids AS [dbo].[GuidIdArray] READONLY,
    @UserId AS UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    CREATE TABLE #Temp
    (
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [UserId] UNIQUEIDENTIFIER NULL
    )

    INSERT INTO #Temp
    SELECT
        [Id],
        [UserId]
    FROM
        [dbo].[UserCipherDetails](@UserId) ucd
        INNER JOIN @Ids ids ON ids.Id = ucd.[Id]
    WHERE
        ucd.[ArchivedDate] IS NOT NULL

    DECLARE @UtcNow DATETIME2(7) = SYSUTCDATETIME();
    UPDATE
        [dbo].[Cipher]
    SET
        [Archives] = JSON_MODIFY(
            COALESCE([Archives], N'{}'),
            CONCAT('$."', @UserId, '"'),
            NULL
        ),
        [RevisionDate] = @UtcNow
    WHERE
        [Id] IN (SELECT [Id] FROM #Temp)

    EXEC [dbo].[User_BumpAccountRevisionDate] @UserId

    DROP TABLE #Temp

    SELECT @UtcNow
END
GO

CREATE OR ALTER PROCEDURE [dbo].[Cipher_Archive]
    @Ids AS [dbo].[GuidIdArray] READONLY,
    @UserId AS UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    CREATE TABLE #Temp
    (
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [UserId] UNIQUEIDENTIFIER NULL
    )

    INSERT INTO #Temp
    SELECT
        [Id],
        [UserId]
    FROM
        [dbo].[UserCipherDetails](@UserId) ucd
        INNER JOIN @Ids ids ON ids.Id = ucd.[Id]
    WHERE
        ucd.[ArchivedDate] IS NOT NULL

    DECLARE @UtcNow DATETIME2(7) = SYSUTCDATETIME();
    UPDATE
        [dbo].[Cipher]
    SET
        [Archives] = JSON_MODIFY(
            COALESCE([Archives], N'{}'),
            CONCAT('$."', @UserId, '"'),
            CONVERT(NVARCHAR(30), @UtcNow, 127)
        ),
        [RevisionDate] = @UtcNow
    WHERE
        [Id] IN (SELECT [Id] FROM #Temp)

    EXEC [dbo].[User_BumpAccountRevisionDate] @UserId

    DROP TABLE #Temp

    SELECT @UtcNow
END
GO
