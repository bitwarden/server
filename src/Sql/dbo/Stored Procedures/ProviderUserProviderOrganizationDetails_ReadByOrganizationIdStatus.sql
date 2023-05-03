CREATE PROCEDURE [dbo].[ProviderUserProviderOrganizationDetails_ReadByOrganizationIdStatus]
    @OrganizationId UNIQUEIDENTIFIER,
    @Status TINYINT
AS
BEGIN
    SET NOCOUNT ON

SELECT
    *
FROM
    [dbo].[ProviderUserProviderOrganizationDetailsView]
WHERE
    [OrganizationId] = @OrganizationId
    AND (@Status IS NULL OR [Status] = @Status)
END