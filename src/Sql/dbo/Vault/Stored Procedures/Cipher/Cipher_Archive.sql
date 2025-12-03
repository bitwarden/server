CREATE PROCEDURE [dbo].[Cipher_Archive]
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
        [dbo].[UserCipherDetails](@UserId)
    WHERE
        [Edit] = 1
      AND [ArchivedDate] IS NULL
      AND [Id] IN (SELECT * FROM @Ids)

    DECLARE @UtcNow DATETIME2(7) = SYSUTCDATETIME();
    UPDATE
        [dbo].[Cipher]
    SET
        [Archives] = JSON_MODIFY(
            COALESCE([Archives], N'{}'),
            '$."' + CONVERT(NVARCHAR(36), @UserId) + '"',
            CONVERT(NVARCHAR(30), @UtcNow, 127)
        ),
        [RevisionDate] = @UtcNow
    WHERE
        [Id] IN (SELECT [Id] FROM #Temp)

    EXEC [dbo].[User_BumpAccountRevisionDate] @UserId

    DROP TABLE #Temp

    SELECT @UtcNow
END
