CREATE PROCEDURE [dbo].[Cipher_Archive]
    @Ids dbo.GuidIdArray READONLY,
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @UtcNow DATETIME2(7) = SYSUTCDATETIME();

    WITH CipherIdsToArchive AS
    (
        SELECT DISTINCT C.Id
        FROM [dbo].[Cipher] C
        INNER JOIN @Ids I ON C.Id = I.[Id]
        WHERE (C.[UserId] = @UserId)
    )
    INSERT INTO [dbo].[CipherArchive] (CipherId, UserId, ArchivedDate)
    SELECT Cta.Id, @UserId, @UtcNow
    FROM CipherIdsToArchive Cta
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM [dbo].[CipherArchive] Ca
        WHERE Ca.CipherId = Cta.Id
          AND Ca.UserId = @UserId
    );

    EXEC [dbo].[User_BumpAccountRevisionDate] @UserId

    SELECT @UtcNow
END
