CREATE PROCEDURE [dbo].[OrganizationApiKey_ReadManyByOrganizationIdType]
    @OrganizationId UNIQUEIDENTIFIER,
    @Type TINYINT = NULL
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationApiKeyView]
    WHERE
        [OrganizationId] = @OrganizationId AND
        (@Type IS NULL OR [Type] = @Type)
END
