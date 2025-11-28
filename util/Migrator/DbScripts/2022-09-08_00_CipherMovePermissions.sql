-- Remove check for Edit permission. User should be able to move the cipher to a different folder even if they don't have Edit permissions

ALTER PROCEDURE [dbo].[Cipher_Move]
    @Ids AS [dbo].[GuidIdArray] READONLY,
    @FolderId AS UNIQUEIDENTIFIER,
    @UserId AS UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @UserIdKey VARCHAR(50) = CONCAT('"', @UserId, '"')
    DECLARE @UserIdPath VARCHAR(50) = CONCAT('$.', @UserIdKey)

    ;WITH [IdsToMoveCTE] AS (
        SELECT
            [Id]
        FROM
            [dbo].[UserCipherDetails](@UserId)
        WHERE
            [Id] IN (SELECT * FROM @Ids)
    )
    UPDATE
        [dbo].[Cipher]
    SET
        [Folders] = 
            CASE
            WHEN @FolderId IS NOT NULL AND [Folders] IS NULL THEN
                CONCAT('{', @UserIdKey, ':"', @FolderId, '"', '}')
            WHEN @FolderId IS NOT NULL THEN
                JSON_MODIFY([Folders], @UserIdPath, CAST(@FolderId AS VARCHAR(50)))
            ELSE
                JSON_MODIFY([Folders], @UserIdPath, NULL)
            END
    WHERE
        [Id] IN (SELECT * FROM [IdsToMoveCTE])

    EXEC [dbo].[User_BumpAccountRevisionDate] @UserId
END
GO