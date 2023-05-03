CREATE OR ALTER PROCEDURE [dbo].[Provider_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

SELECT
    P.*
FROM
    [dbo].[ProviderView] P
INNER JOIN
    [dbo].[ProviderOrganization] PO ON PO.[ProviderId] = P.[Id]
WHERE
    PO.[OrganizationId] = @OrganizationId
END
GO