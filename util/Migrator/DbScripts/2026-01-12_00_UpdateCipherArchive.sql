CREATE OR ALTER PROCEDURE [dbo].[Cipher_Unarchive]
    @Ids AS [dbo].[GuidIdArray] READONLY,
    @UserId AS UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @UtcNow DATETIME2(7) = SYSUTCDATETIME();

    ;WITH Target AS
    (
        SELECT ucd.[Id]
        FROM [dbo].[UserCipherDetails](@UserId) AS ucd
        INNER JOIN @Ids AS ids
            ON ids.[Id] = ucd.[Id]
        WHERE ucd.[ArchivedDate] IS NOT NULL
    )
    UPDATE c
    SET
        [Archives] = JSON_MODIFY(
            COALESCE(c.[Archives], N'{}'),
            CONCAT('$."', @UserId, '"'),
            NULL
        ),
        [RevisionDate] = @UtcNow
    FROM [dbo].[Cipher] AS c
    INNER JOIN Target AS t
        ON t.[Id] = c.[Id];

    EXEC [dbo].[User_BumpAccountRevisionDate] @UserId;

    SELECT @UtcNow;
END
GO

CREATE OR ALTER PROCEDURE [dbo].[Cipher_Archive]
    @Ids AS [dbo].[GuidIdArray] READONLY,
    @UserId AS UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @UtcNow DATETIME2(7) = SYSUTCDATETIME();

    ;WITH Target AS
    (
        SELECT ucd.[Id]
        FROM [dbo].[UserCipherDetails](@UserId) AS ucd
        INNER JOIN @Ids AS ids
            ON ids.[Id] = ucd.[Id]
        WHERE ucd.[ArchivedDate] IS NULL
    )
    UPDATE c
    SET
        [Archives] = JSON_MODIFY(
            COALESCE(c.[Archives], N'{}'),
            CONCAT('$."', @UserId, '"'),
            CONVERT(NVARCHAR(30), @UtcNow, 127)
        ),
        [RevisionDate] = @UtcNow
    FROM [dbo].[Cipher] AS c
    INNER JOIN Target AS t
        ON t.[Id] = c.[Id];

    EXEC [dbo].[User_BumpAccountRevisionDate] @UserId;

    SELECT @UtcNow;
END
GO

