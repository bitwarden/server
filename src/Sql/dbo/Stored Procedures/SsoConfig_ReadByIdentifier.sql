CREATE PROCEDURE [dbo].[SsoConfig_ReadByIdentifier]
    @Identifier NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        SSO.*
    FROM
        [dbo].[SsoConfigView] SSO
    INNER JOIN
        [dbo].[Organization] O ON O.[Id] = SSO.[OrganizationId]
        AND O.[Identifier] = @Identifier
END
