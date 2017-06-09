CREATE PROCEDURE [dbo].[Cipher_Delete]
    @Ids AS [dbo].[GuidIdArray] READONLY,
    @UserId AS UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    ;WITH [IdsToDeleteCTE] AS (
        SELECT
            [Id]
        FROM
            [dbo].[UserCipherDetails](@UserId)
        WHERE
            [Edit] = 1
            AND [Id] IN (SELECT * FROM @Ids)
    )
    DELETE
    FROM
        [dbo].[Cipher]
    WHERE
        [Id] IN (SELECT * FROM [IdsToDeleteCTE])

    EXEC [dbo].[User_BumpAccountRevisionDate] @UserId
    -- TODO: What if some that were deleted were organization ciphers? Then bump by org ids.
END