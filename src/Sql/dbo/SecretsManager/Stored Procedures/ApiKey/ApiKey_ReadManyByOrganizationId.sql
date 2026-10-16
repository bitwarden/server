CREATE PROCEDURE [dbo].[ApiKey_ReadManyByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[ApiKeyView]
    WHERE
        [OrganizationId] = @OrganizationId
END
