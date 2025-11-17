CREATE PROCEDURE [dbo].[CipherDetails_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        c.*,
        ca.ArchivedDate
    FROM
        [dbo].[UserCipherDetails](@UserId) AS c
        LEFT JOIN [dbo].[CipherArchive] AS ca
            ON ca.CipherId = c.Id
           AND ca.UserId = @UserId
END