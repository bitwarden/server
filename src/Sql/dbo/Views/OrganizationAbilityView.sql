CREATE VIEW [dbo].[OrganizationAbilityView]
AS
SELECT
    [Id],
    [UseEvents],
    [Use2fa],
    IIF([Use2fa] = 1 AND [TwoFactorProviders] IS NOT NULL AND [TwoFactorProviders] != '{}', 1, 0) AS [Using2fa],
    [UsersGetPremium],
    [Enabled],
    [UseSso],
    [UseKeyConnector],
    [UseScim],
    [UseResetPassword],
    [UseCustomPermissions],
    [UsePolicies],
    [LimitCollectionCreation],
    [LimitCollectionDeletion],
    [LimitItemDeletion],
    [AllowAdminAccessToAllCollectionItems],
    [UseRiskInsights],
    [UseOrganizationDomains],
    [UseAdminSponsoredFamilies],
    [UseAutomaticUserConfirmation],
    [UseDisableSmAdsForUsers],
    [UsePhishingBlocker],
    [UseMyItems]
FROM
    [dbo].[Organization]
