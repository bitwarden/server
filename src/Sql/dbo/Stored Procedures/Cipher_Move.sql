CREATE PROCEDURE [dbo].[Cipher_Move]
    @Ids AS [dbo].[GuidIdArray] READONLY,
    @FolderId AS UNIQUEIDENTIFIER,
    @UserId AS UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    ;WITH [CTE] AS (
        SELECT
            [Id],
            [Edit],
            [FolderId]
        FROM
            [dbo].[UserCipherDetails](@UserId)
    )
    UPDATE
        [CTE]
    SET
        [FolderId] = @FolderId
    WHERE
        [Edit] = 1
        AND [Id] IN (@Ids)

    EXEC [dbo].[User_BumpAccountRevisionDate] @UserId
    -- TODO: What if some that were updated were organization ciphers? Then bump by org ids.
END