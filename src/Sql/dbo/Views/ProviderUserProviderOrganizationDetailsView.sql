CREATE VIEW [dbo].[ProviderUserProviderOrganizationDetailsView]
AS
SELECT
    PU.[UserId],
    PO.[OrganizationId],
    O.[Name],
    O.[Enabled],
    O.[UsePolicies],
    O.[UseSso],
    O.[UseKeyConnector],
    O.[UseScim],
    O.[UseGroups],
    O.[UseDirectory],
    O.[UseEvents],
    O.[UseTotp],
    O.[Use2fa],
    O.[UseApi],
    O.[UseResetPassword],
    O.[SelfHost],
    O.[UsersGetPremium],
    O.[UseCustomPermissions],
    O.[Seats],
    O.[MaxCollections],
    O.[MaxStorageGb],
    O.[Identifier],
    PO.[Key],
    O.[PublicKey],
    O.[PrivateKey],
    PU.[Status],
    PU.[Type],
    PO.[ProviderId],
    PU.[Id] ProviderUserId,
    P.[Name] ProviderName,
    O.[PlanType],
    O.[LimitCollectionCreationDeletion], -- Deprecated https://bitwarden.atlassian.net/browse/PM-10863
    O.[LimitCollectionCreation],
    O.[LimitCollectionDeletion],
    O.[AllowAdminAccessToAllCollectionItems],
    O.[UseRiskInsights]
FROM
    [dbo].[ProviderUser] PU
INNER JOIN
    [dbo].[ProviderOrganization] PO ON PO.[ProviderId] = PU.[ProviderId]
INNER JOIN
    [dbo].[Organization] O ON O.[Id] = PO.[OrganizationId]
INNER JOIN
    [dbo].[Provider] P ON P.[Id] = PU.[ProviderId]
