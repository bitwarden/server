CREATE PROCEDURE [dbo].[SubvaultCipher_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        SC.*
    FROM
        [dbo].[SubvaultCipher] SC
    INNER JOIN
        [dbo].[Subvault] S ON S.[Id] = SC.[SubvaultId]
    WHERE
        S.[OrganizationId] = @OrganizationId
END