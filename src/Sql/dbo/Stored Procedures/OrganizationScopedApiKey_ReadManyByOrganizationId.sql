CREATE PROCEDURE [dbo].[OrganizationScopedApiKey_ReadManyByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationScopedApiKeyView]
    WHERE
        [OrganizationId] = @OrganizationId
END
