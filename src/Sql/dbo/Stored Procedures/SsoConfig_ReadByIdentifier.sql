CREATE PROCEDURE [dbo].[SsoConfig_ReadByIdentifier]
    @Identifier NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON

    SELECT TOP 1
        SSO.*,
        O.[Identifier]
    FROM
        [dbo].[SsoConfigView] SSO
    INNER JOIN
        [dbo].[Organization] O ON O.[Id] = SSO.[OrganizationId]
    WHERE
        O.[Identifier] = @Identifier
END
