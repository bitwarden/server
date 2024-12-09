    /* Alter view to include UseRiskInsights */
    CREATE OR ALTER VIEW [dbo].[OrganizationUserOrganizationDetailsView]
    AS
    SELECT
        OU.[UserId],
        OU.[OrganizationId],
        OU.[Id] OrganizationUserId,
        O.[Name],
        O.[Enabled],
        O.[PlanType],
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
        O.[UseSecretsManager],
        O.[Seats],
        O.[MaxCollections],
        O.[MaxStorageGb],
        O.[Identifier],
        OU.[Key],
        OU.[ResetPasswordKey],
        O.[PublicKey],
        O.[PrivateKey],
        OU.[Status],
        OU.[Type],
        SU.[ExternalId] SsoExternalId,
        OU.[Permissions],
        PO.[ProviderId],
        P.[Name] ProviderName,
        P.[Type] ProviderType,
        SS.[Data] SsoConfig,
        OS.[FriendlyName] FamilySponsorshipFriendlyName,
        OS.[LastSyncDate] FamilySponsorshipLastSyncDate,
        OS.[ToDelete] FamilySponsorshipToDelete,
        OS.[ValidUntil] FamilySponsorshipValidUntil,
        OU.[AccessSecretsManager],
        O.[UsePasswordManager],
        O.[SmSeats],
        O.[SmServiceAccounts],
        O.[LimitCollectionCreationDeletion], -- Deprecated https://bitwarden.atlassian.net/browse/PM-10863
        O.[LimitCollectionCreation],
        O.[LimitCollectionDeletion],
        O.[AllowAdminAccessToAllCollectionItems],
        O.[UseRiskInsights]
    FROM
        [dbo].[OrganizationUser] OU
    LEFT JOIN
        [dbo].[Organization] O ON O.[Id] = OU.[OrganizationId]
    LEFT JOIN
        [dbo].[SsoUser] SU ON SU.[UserId] = OU.[UserId] AND SU.[OrganizationId] = OU.[OrganizationId]
    LEFT JOIN
        [dbo].[ProviderOrganization] PO ON PO.[OrganizationId] = O.[Id]
    LEFT JOIN
        [dbo].[Provider] P ON P.[Id] = PO.[ProviderId]
    LEFT JOIN
        [dbo].[SsoConfig] SS ON SS.[OrganizationId] = OU.[OrganizationId]
    LEFT JOIN
        [dbo].[OrganizationSponsorship] OS ON OS.[SponsoringOrganizationUserID] = OU.[Id]
    GO

    /* Alter this view to include UseRiskInsights column to the query */
    CREATE OR ALTER VIEW [dbo].[ProviderUserProviderOrganizationDetailsView]
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
    GO


    --Manually refresh [dbo].[OrganizationUserOrganizationDetailsView]
    IF OBJECT_ID('[dbo].[OrganizationUserOrganizationDetailsView]') IS NOT NULL
        BEGIN
            EXECUTE sp_refreshsqlmodule N'[dbo].[OrganizationUserOrganizationDetailsView]';
        END
    GO

    --Manually refresh [dbo].[ProviderUserProviderOrganizationDetailsView]
    IF OBJECT_ID('[dbo].[ProviderUserProviderOrganizationDetailsView]') IS NOT NULL
        BEGIN
            EXECUTE sp_refreshsqlmodule N'[dbo].[ProviderUserProviderOrganizationDetailsView]';
        END
    GO

    --Manually refresh [dbo].[OrganizationView]
    IF OBJECT_ID('[dbo].[OrganizationView]') IS NOT NULL
        BEGIN
            EXECUTE sp_refreshsqlmodule N'[dbo].[OrganizationView]';
        END
    GO
    