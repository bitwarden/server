CREATE PROCEDURE [dbo].[SsoConfig_ReadByOrganizationId]
@OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[SsoConfigView]
    WHERE
        [OrganizationId] = @OrganizationId
END