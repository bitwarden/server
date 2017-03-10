CREATE PROCEDURE [dbo].[Subvault_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        S.*
    FROM
        [dbo].[SubvaultView] S
    WHERE
        S.[OrganizationId] = @OrganizationId
END