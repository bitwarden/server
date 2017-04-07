CREATE PROCEDURE [dbo].[Subvault_ReadCountByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        COUNT(1)
    FROM
        [dbo].[Subvault]
    WHERE
        [OrganizationId] = @OrganizationId
END