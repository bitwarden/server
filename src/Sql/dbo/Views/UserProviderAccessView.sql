CREATE VIEW [dbo].[UserProviderAccessView]
AS
SELECT DISTINCT
    PU.[UserId],
    PO.[OrganizationId]
FROM
    [dbo].[ProviderUserView] PU
INNER JOIN
    [dbo].[ProviderOrganizationView] PO ON PO.[ProviderId] = PU.[ProviderId]
