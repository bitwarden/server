CREATE PROCEDURE [dbo].[PamTargetSystem_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[PamTargetSystemView]
    WHERE
        [OrganizationId] = @OrganizationId
END
