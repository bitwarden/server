CREATE PROCEDURE [dbo].[SsoConfig_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT TOP 1
        *
    FROM
        [dbo].[SsoConfigView]
    WHERE
        [OrganizationId] = @OrganizationId
END
