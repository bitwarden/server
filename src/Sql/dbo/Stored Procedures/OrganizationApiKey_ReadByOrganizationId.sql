CREATE PROCEDURE [dbo].[OrganizationApiKey_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationApiKeyView]
    WHERE
        [OrganizationId] = @OrganizationId
END