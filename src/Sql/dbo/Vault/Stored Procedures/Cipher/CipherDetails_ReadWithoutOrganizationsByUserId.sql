CREATE PROCEDURE [dbo].[CipherDetails_ReadWithoutOrganizationsByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        c.*,
        1 [Edit],
        1 [ViewPassword],
        1 [Manage],
        0 [OrganizationUseTotp],
        ca.ArchivedDate
    FROM
        [dbo].[CipherDetails](@UserId) AS c
        LEFT JOIN [dbo].[CipherArchive] AS ca
            ON ca.CipherId = c.Id
           AND ca.UserId = @UserId
    WHERE
        c.[UserId] = @UserId
END