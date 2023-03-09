ALTER VIEW [dbo].[ProviderUserProviderOrganizationDetailsView]
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
    P.[Type] ProviderType,
    O.[PlanType]
FROM
    [dbo].[ProviderUser] PU
    INNER JOIN
    [dbo].[ProviderOrganization] PO ON PO.[ProviderId] = PU.[ProviderId]
    INNER JOIN
    [dbo].[Organization] O ON O.[Id] = PO.[OrganizationId]
    INNER JOIN
    [dbo].[Provider] P ON P.[Id] = PU.[ProviderId]
GO

ALTER PROCEDURE [dbo].[ProviderUserProviderOrganizationDetails_ReadByUserIdStatus]
    @UserId UNIQUEIDENTIFIER,
    @Status TINYINT
AS
BEGIN
    SET NOCOUNT ON

SELECT
    *
FROM
    [dbo].[ProviderUserProviderOrganizationDetailsView]
WHERE
    [UserId] = @UserId
    AND (@Status IS NULL OR [Status] = @Status)
END
GO

ALTER VIEW [dbo].[OrganizationUserOrganizationDetailsView]
AS
SELECT
    OU.[UserId],
    OU.[OrganizationId],
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
    OU.[AccessSecretsManager]
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

ALTER PROCEDURE [dbo].[OrganizationUserOrganizationDetails_ReadByUserIdStatus]
    @UserId UNIQUEIDENTIFIER,
    @Status SMALLINT
AS
BEGIN
    SET NOCOUNT ON

SELECT
    *
FROM
    [dbo].[OrganizationUserOrganizationDetailsView]
WHERE
    [UserId] = @UserId
    AND (@Status IS NULL OR [Status] = @Status)
END
GO

ALTER PROCEDURE [dbo].[OrganizationUserOrganizationDetails_ReadByUserIdStatusOrganizationId]
    @UserId UNIQUEIDENTIFIER,
    @Status SMALLINT,
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

SELECT
    *
FROM
    [dbo].[OrganizationUserOrganizationDetailsView]
WHERE
    [UserId] = @UserId
    AND [OrganizationId] = @OrganizationId
    AND (@Status IS NULL OR [Status] = @Status)
END
GO