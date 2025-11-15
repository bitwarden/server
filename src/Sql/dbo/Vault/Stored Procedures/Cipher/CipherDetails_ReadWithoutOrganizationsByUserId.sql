CREATE PROCEDURE [dbo].[CipherDetails_ReadWithoutOrganizationsByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *,
        1 [Edit],
        1 [ViewPassword],
        1 [Manage],
        0 [OrganizationUseTotp]
    FROM
        [dbo].[CipherDetails](@UserId)
    LEFT JOIN [dbo].[CipherArchive] ca
        ON ca.CipherId = c.Id
       AND ca.UserId = @UserId
    WHERE
        [UserId] = @UserId
END