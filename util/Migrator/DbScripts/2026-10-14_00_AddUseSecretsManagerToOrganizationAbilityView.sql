-- SM-620: Expose UseSecretsManager through OrganizationAbilityView so the cached
-- organization ability can gate Secrets Manager access without reading the
-- Organization row on every request.
CREATE OR ALTER VIEW [dbo].[OrganizationAbilityView]
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
    [UseMyItems],
    [UseInviteLinks],
    [UsePam],
    [UseSecretsManager]
FROM
    [dbo].[Organization]
GO

-- Organization_ReadAbilityById selects * from the view, so its cached column
-- metadata must be refreshed to include the new column.
IF OBJECT_ID('[dbo].[Organization_ReadAbilityById]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[Organization_ReadAbilityById]'
END
GO
