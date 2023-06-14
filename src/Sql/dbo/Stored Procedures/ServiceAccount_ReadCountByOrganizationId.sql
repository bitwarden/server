CREATE PROCEDURE [dbo].[ServiceAccount_ReadCountByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        COUNT(1)
    FROM
        [dbo].[ServiceAccount]
    WHERE
        OrganizationId = @OrganizationId
END