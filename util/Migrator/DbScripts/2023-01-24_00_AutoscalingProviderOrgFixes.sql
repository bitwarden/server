-- SG-992 changes: add planType to provider orgs
CREATE OR ALTER  VIEW [dbo].[ProviderUserProviderOrganizationDetailsView]
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
    O.[PlanType] -- new prop
FROM
    [dbo].[ProviderUser] PU
    INNER JOIN
    [dbo].[ProviderOrganization] PO ON PO.[ProviderId] = PU.[ProviderId]
    INNER JOIN
    [dbo].[Organization] O ON O.[Id] = PO.[OrganizationId]
    INNER JOIN
    [dbo].[Provider] P ON P.[Id] = PU.[ProviderId]
    GO

        
-- Refresh metadata of stored procs & functions that use the updated view
IF OBJECT_ID('[dbo].[ProviderUserProviderOrganizationDetails_ReadByUserIdStatus]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[ProviderUserProviderOrganizationDetails_ReadByUserIdStatus]';
END
GO


-- EC-591 / SG-996 changes: add optional status to stored proc
CREATE OR ALTER PROCEDURE [dbo].[ProviderUserUserDetails_ReadByProviderId]
@ProviderId UNIQUEIDENTIFIER,
@Status TINYINT = NULL  -- new: this is required to be backwards compatible
AS
BEGIN
    SET NOCOUNT ON

SELECT
    *
FROM
    [dbo].[ProviderUserUserDetailsView]
WHERE
    [ProviderId] = @ProviderId
  AND [Status] = COALESCE(@Status, [Status])  -- new
END
GO
