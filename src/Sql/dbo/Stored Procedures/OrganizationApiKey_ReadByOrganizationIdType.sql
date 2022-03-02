CREATE PROCEDURE [dbo].[OrganizationApiKey_ReadByOrganizationIdType]
    @OrganizationId UNIQUEIDENTIFIER,
    @Type TINYINT
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationApiKeyView]
    WHERE
        [OrganizationId] = @OrganizationId AND
        [Type] = @Type
END
