CREATE OR ALTER VIEW [dbo].[ProviderOrganizationProviderDetailsView]
AS
SELECT
    PO.[Id],
    PO.[ProviderId],
    PO.[OrganizationId],
    P.[Name] as ProviderName,
    P.[Type] as ProviderType,
    P.[Status] as ProviderStatus,
    P.[BillingEmail] as ProviderBillingEmail
FROM
    [dbo].[ProviderOrganization] PO
LEFT JOIN
    [dbo].[Provider] P ON P.[Id] = PO.[ProviderId]
GO

CREATE OR ALTER PROCEDURE [dbo].[ProviderOrganizationProviderDetails_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

SELECT
    *
FROM
    [dbo].[ProviderOrganizationProviderDetailsView]
WHERE
    [OrganizationId] = @OrganizationId
END
GO