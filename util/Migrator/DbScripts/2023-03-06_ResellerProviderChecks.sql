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
    @Status TINYINT,
    @ProviderType TINYINT = 0
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
  AND (@ProviderType IS NULL OR [ProviderType] = @ProviderType)
END
GO