CREATE PROCEDURE [dbo].[CipherDetails_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[UserCipherDetails](@UserId)
    LEFT JOIN [dbo].[CipherArchive] ca
        ON ca.CipherId = c.Id
       AND ca.UserId = @UserId
    WHERE
END