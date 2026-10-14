CREATE PROCEDURE [dbo].[PamAccessConnector_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[PamAccessConnectorView]
    WHERE
        [OrganizationId] = @OrganizationId
END
