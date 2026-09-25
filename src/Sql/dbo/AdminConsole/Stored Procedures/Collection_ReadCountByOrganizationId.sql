CREATE PROCEDURE [dbo].[Collection_ReadCountByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        COUNT(1)
    FROM
        [dbo].[Collection]
    WHERE
        [OrganizationId] = @OrganizationId
END