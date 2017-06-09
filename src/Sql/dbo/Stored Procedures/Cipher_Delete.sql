CREATE PROCEDURE [dbo].[Cipher_Delete]
    @Ids AS [dbo].[GuidIdArray] READONLY,
    @UserId AS UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    ;WITH [CTE] AS (
        SELECT
            [Id],
            [Edit]
        FROM
            [dbo].[UserCipherDetails](@UserId)
    )
    DELETE
    FROM
        [CTE]
    WHERE
        [Edit] = 1
        AND [Id] IN (@Ids)

    EXEC [dbo].[User_BumpAccountRevisionDate] @UserId
    -- TODO: What if some that were deleted were organization ciphers? Then bump by org ids.
END