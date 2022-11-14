CREATE PROCEDURE [dbo].[Group_ReadCountByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        COUNT(1)
    FROM
        [dbo].[Group]
    WHERE
        [OrganizationId] = @OrganizationId
END
