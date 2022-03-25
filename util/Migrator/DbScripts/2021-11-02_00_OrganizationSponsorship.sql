-- Create Organization Sponsorships table
IF OBJECT_ID('[dbo].[OrganizationSponsorship]') IS NULL
BEGIN
CREATE TABLE [dbo].[OrganizationSponsorship] (
    [Id]                            UNIQUEIDENTIFIER NOT NULL,
    [InstallationId]                UNIQUEIDENTIFIER NULL,
    [SponsoringOrganizationId]      UNIQUEIDENTIFIER NULL,
    [SponsoringOrganizationUserID]  UNIQUEIDENTIFIER NULL,
    [SponsoredOrganizationId]       UNIQUEIDENTIFIER NULL,
    [FriendlyName]                  NVARCHAR(256)    NULL,
    [OfferedToEmail]                NVARCHAR (256)   NULL,
    [PlanSponsorshipType]           TINYINT          NULL,
    [CloudSponsor]                  BIT              NULL,
    [LastSyncDate]                  DATETIME2 (7)    NULL,
    [TimesRenewedWithoutValidation] TINYINT          DEFAULT 0,
    [SponsorshipLapsedDate]         DATETIME2 (7)    NULL,
    CONSTRAINT [PK_OrganizationSponsorship] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_OrganizationSponsorship_InstallationId] FOREIGN KEY ([InstallationId]) REFERENCES [dbo].[Installation] ([Id]),
    CONSTRAINT [FK_OrganizationSponsorship_SponsoringOrg] FOREIGN KEY ([SponsoringOrganizationId]) REFERENCES [dbo].[Organization] ([Id]),
    CONSTRAINT [FK_OrganizationSponsorship_SponsoredOrg] FOREIGN KEY ([SponsoredOrganizationId]) REFERENCES [dbo].[Organization] ([Id]),
);
END
GO


-- Create indexes
IF NOT EXISTS(SELECT name FROM sys.indexes WHERE name = 'IX_OrganizationSponsorship_InstallationId')
BEGIN
CREATE NONCLUSTERED INDEX [IX_OrganizationSponsorship_InstallationId]
    ON [dbo].[OrganizationSponsorship]([InstallationId] ASC)
    WHERE [InstallationId] IS NOT NULL;
END
GO

IF NOT EXISTS(SELECT name FROM sys.indexes WHERE name = 'IX_OrganizationSponsorship_SponsoringOrganizationId')
BEGIN
CREATE NONCLUSTERED INDEX [IX_OrganizationSponsorship_SponsoringOrganizationId]
    ON [dbo].[OrganizationSponsorship]([SponsoringOrganizationId] ASC)
    WHERE [SponsoringOrganizationId] IS NOT NULL;
END
GO

IF NOT EXISTS(SELECT name FROM sys.indexes WHERE name = 'IX_OrganizationSponsorship_SponsoringOrganizationUserId')
BEGIN
CREATE NONCLUSTERED INDEX [IX_OrganizationSponsorship_SponsoringOrganizationUserId]
    ON [dbo].[OrganizationSponsorship]([SponsoringOrganizationUserID] ASC)
    WHERE [SponsoringOrganizationUserID] IS NOT NULL;
END
GO

IF NOT EXISTS(SELECT name FROM sys.indexes WHERE name = 'IX_OrganizationSponsorship_OfferedToEmail')
BEGIN
CREATE NONCLUSTERED INDEX [IX_OrganizationSponsorship_OfferedToEmail]
    ON [dbo].[OrganizationSponsorship]([OfferedToEmail] ASC)
    WHERE [OfferedToEmail] IS NOT NULL;
END
GO

IF NOT EXISTS(SELECT name FROM sys.indexes WHERE name = 'IX_OrganizationSponsorship_SponsoredOrganizationID')
BEGIN
CREATE NONCLUSTERED INDEX [IX_OrganizationSponsorship_SponsoredOrganizationID]
    ON [dbo].[OrganizationSponsorship]([SponsoredOrganizationId] ASC)
    WHERE [SponsoredOrganizationId] IS NOT NULL;
END
GO


-- Create View
IF EXISTS(SELECT * FROM sys.views WHERE [Name] = 'OrganizationSponsorshipView')
BEGIN
    DROP VIEW [dbo].[OrganizationSponsorshipView];
END
GO

CREATE VIEW [dbo].[OrganizationSponsorshipView]
AS
SELECT
    *
FROM
    [dbo].[OrganizationSponsorship]
GO


-- OrganizationSponsorship_ReadById
IF OBJECT_ID('[dbo].[OrganizationSponsorship_ReadById]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationSponsorship_ReadById]
END
GO

CREATE PROCEDURE [dbo].[OrganizationSponsorship_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationSponsorshipView]
    WHERE
        [Id] = @Id
END
GO


-- OrganizationSponsorship_Create
IF OBJECT_ID('[dbo].[OrganizationSponsorship_Create]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationSponsorship_Create]
END
GO

CREATE PROCEDURE [dbo].[OrganizationSponsorship_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @InstallationId UNIQUEIDENTIFIER,
    @SponsoringOrganizationId UNIQUEIDENTIFIER,
    @SponsoringOrganizationUserID UNIQUEIDENTIFIER,
    @SponsoredOrganizationId UNIQUEIDENTIFIER,
    @FriendlyName NVARCHAR(256),
    @OfferedToEmail NVARCHAR(256),
    @PlanSponsorshipType TINYINT,
    @CloudSponsor BIT,
    @LastSyncDate DATETIME2 (7),
    @TimesRenewedWithoutValidation TINYINT,
    @SponsorshipLapsedDate DATETIME2 (7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[OrganizationSponsorship]
    (
        [Id],
        [InstallationId],
        [SponsoringOrganizationId],
        [SponsoringOrganizationUserID],
        [SponsoredOrganizationId],
        [FriendlyName],
        [OfferedToEmail],
        [PlanSponsorshipType],
        [CloudSponsor],
        [LastSyncDate],
        [TimesRenewedWithoutValidation],
        [SponsorshipLapsedDate]
    )
    VALUES
    (
        @Id,
        @InstallationId,
        @SponsoringOrganizationId,
        @SponsoringOrganizationUserID,
        @SponsoredOrganizationId,
        @FriendlyName,
        @OfferedToEmail,
        @PlanSponsorshipType,
        @CloudSponsor,
        @LastSyncDate,
        @TimesRenewedWithoutValidation,
        @SponsorshipLapsedDate
    )
END
GO

-- OrganizationSponsorship_Update
IF OBJECT_ID('[dbo].[OrganizationSponsorship_Update]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationSponsorship_Update]
END
GO

CREATE PROCEDURE [dbo].[OrganizationSponsorship_Update]
    @Id UNIQUEIDENTIFIER,
    @InstallationId UNIQUEIDENTIFIER,
    @SponsoringOrganizationId UNIQUEIDENTIFIER,
    @SponsoringOrganizationUserID UNIQUEIDENTIFIER,
    @SponsoredOrganizationId UNIQUEIDENTIFIER,
    @FriendlyName NVARCHAR(256),
    @OfferedToEmail NVARCHAR(256),
    @PlanSponsorshipType TINYINT,
    @CloudSponsor BIT,
    @LastSyncDate DATETIME2 (7),
    @TimesRenewedWithoutValidation TINYINT,
    @SponsorshipLapsedDate DATETIME2 (7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[OrganizationSponsorship]
    SET
        [InstallationId] = @InstallationId,
        [SponsoringOrganizationId] = @SponsoringOrganizationId,
        [SponsoringOrganizationUserID] = @SponsoringOrganizationUserID,
        [SponsoredOrganizationId] = @SponsoredOrganizationId,
        [FriendlyName] = @FriendlyName,
        [OfferedToEmail] = @OfferedToEmail,
        [PlanSponsorshipType] = @PlanSponsorshipType,
        [CloudSponsor] = @CloudSponsor,
        [LastSyncDate] = @LastSyncDate,
        [TimesRenewedWithoutValidation] = @TimesRenewedWithoutValidation,
        [SponsorshipLapsedDate] = @SponsorshipLapsedDate
    WHERE
        [Id] = @Id
END
GO


-- OrganizationSponsorship_DeleteById
IF OBJECT_ID('[dbo].[OrganizationSponsorship_DeleteById]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationSponsorship_DeleteById]
END
GO

CREATE PROCEDURE [dbo].[OrganizationSponsorship_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    BEGIN TRANSACTION OrgSponsorship_DeleteById

        DELETE
        FROM
            [dbo].[OrganizationSponsorship]
        WHERE
            [Id] = @Id

    COMMIT TRANSACTION OrgSponsorship_DeleteById
END
GO


-- OrganizationSponsorship_ReadBySponsoringOrganizationUserId
IF OBJECT_ID('[dbo].[OrganizationSponsorship_ReadBySponsoringOrganizationUserId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationSponsorship_ReadBySponsoringOrganizationUserId]
END
GO

CREATE PROCEDURE [dbo].[OrganizationSponsorship_ReadBySponsoringOrganizationUserId]
    @SponsoringOrganizationUserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationSponsorshipView]
    WHERE
        [SponsoringOrganizationUserId] = @SponsoringOrganizationUserId
END
GO



-- OrganizationSponsorship_ReadBySponsoredOrganizationId
IF OBJECT_ID('[dbo].[OrganizationSponsorship_ReadBySponsoredOrganizationId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationSponsorship_ReadBySponsoredOrganizationId]
END
GO

CREATE PROCEDURE [dbo].[OrganizationSponsorship_ReadBySponsoredOrganizationId]
    @SponsoredOrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationSponsorshipView]
    WHERE
        [SponsoredOrganizationId] = @SponsoredOrganizationId
END
GO

-- OrganizationSponsorship_ReadByOfferedToEmail
IF OBJECT_ID('[dbo].[OrganizationSponsorship_ReadByOfferedToEmail]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationSponsorship_ReadByOfferedToEmail]
END
GO

CREATE PROCEDURE [dbo].[OrganizationSponsorship_ReadByOfferedToEmail]
    @OfferedToEmail NVARCHAR (256) -- Should not be null
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationSponsorshipView]
    WHERE
        [OfferedToEmail] = @OfferedToEmail
END
GO

-- OrganizationSponsorship_OrganizationDeleted
IF OBJECT_ID('[dbo].[OrganizationSponsorship_OrganizationDeleted]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationSponsorship_OrganizationDeleted]
END
GO

CREATE PROCEDURE [dbo].[OrganizationSponsorship_OrganizationDeleted]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[OrganizationSponsorship]
    SET
        [SponsoringOrganizationId] = NULL
    WHERE
        [SponsoringOrganizationId] = @OrganizationId AND
        [CloudSponsor] = 0

    UPDATE
        [dbo].[OrganizationSponsorship]
    SET
        [SponsoredOrganizationId] = NULL
    WHERE
        [SponsoredOrganizationId] = @OrganizationId AND
        [CloudSponsor] = 0

    DELETE
    FROM
        [dbo].[OrganizationSponsorship]
    WHERE
        [CloudSponsor] = 1 AND
        ([SponsoredOrganizationId] = @OrganizationId OR
         [SponsoringOrganizationId] = @OrganizationId)
END
GO

-- OrganizationSponsorship_OrganizationUserDeleted
IF OBJECT_ID('[dbo].[OrganizationSponsorship_OrganizationUserDeleted]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationSponsorship_OrganizationUserDeleted]
END
GO

CREATE PROCEDURE [dbo].[OrganizationSponsorship_OrganizationUserDeleted]
    @OrganizationUserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[OrganizationSponsorship]
    WHERE
        [SponsoringOrganizationUserId] = @OrganizationUserId
END
GO

-- OrganizationSponsorship_OrganizationUsersDeleted
IF OBJECT_ID('[dbo].[OrganizationSponsorship_OrganizationUsersDeleted]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationSponsorship_OrganizationUsersDeleted]
END
GO

CREATE PROCEDURE [dbo].[OrganizationSponsorship_OrganizationUsersDeleted]
    @SponsoringOrganizationUserIds [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @BatchSize INT = 100

    WHILE @BatchSize > 0
        BEGIN
        BEGIN TRANSACTION OS_DeleteMany_OUs

        DELETE TOP(@BatchSize) OS
        FROM
            [dbo].[OrganizationSponsorship] OS
        INNER JOIN
            @SponsoringOrganizationUserIds I ON I.Id = OS.SponsoringOrganizationUserId

        SET @BatchSize = @@ROWCOUNT

        COMMIT TRANSACTION OS_DeleteMany_OUs
    END
END
GO

-- Update Organization delete sprocs to handle organization sponsorships
IF OBJECT_ID('[dbo].[Organization_DeleteById]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Organization_DeleteById]
END
GO

CREATE PROCEDURE [dbo].[Organization_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @Id

    DECLARE @BatchSize INT = 100
    WHILE @BatchSize > 0
    BEGIN
        BEGIN TRANSACTION Organization_DeleteById_Ciphers

        DELETE TOP(@BatchSize)
        FROM
            [dbo].[Cipher]
        WHERE
            [UserId] IS NULL
            AND [OrganizationId] = @Id

        SET @BatchSize = @@ROWCOUNT

        COMMIT TRANSACTION Organization_DeleteById_Ciphers
    END

    BEGIN TRANSACTION Organization_DeleteById

    DELETE
    FROM
        [dbo].[SsoUser]
    WHERE
        [OrganizationId] = @Id

    DELETE
    FROM
        [dbo].[SsoConfig]
    WHERE
        [OrganizationId] = @Id

    DELETE CU
    FROM 
        [dbo].[CollectionUser] CU
    INNER JOIN 
        [dbo].[OrganizationUser] OU ON [CU].[OrganizationUserId] = [OU].[Id]
    WHERE 
        [OU].[OrganizationId] = @Id

    DELETE
    FROM 
        [dbo].[OrganizationUser]
    WHERE 
        [OrganizationId] = @Id

    DELETE
    FROM
         [dbo].[ProviderOrganization]
    WHERE
        [OrganizationId] = @Id

    EXEC[dbo].[OrganizationSponsorship_OrganizationDeleted] @Id

    DELETE
    FROM
        [dbo].[Organization]
    WHERE
        [Id] = @Id

    COMMIT TRANSACTION Organization_DeleteById
END
GO

-- Update Organization User delete sprocs to handle organization sponsorships
IF OBJECT_ID('[dbo].[OrganizationUser_DeleteById]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationUser_DeleteById]
END
GO

CREATE PROCEDURE [dbo].[OrganizationUser_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON
    
    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationUserId] @Id
    
    DECLARE @OrganizationId UNIQUEIDENTIFIER
    DECLARE @UserId UNIQUEIDENTIFIER

    SELECT
        @OrganizationId = [OrganizationId],
        @UserId = [UserId]
    FROM
        [dbo].[OrganizationUser]
    WHERE
        [Id] = @Id

    IF @OrganizationId IS NOT NULL AND @UserId IS NOT NULL
    BEGIN
        EXEC [dbo].[SsoUser_Delete] @UserId, @OrganizationId
    END

    DELETE
    FROM
        [dbo].[CollectionUser]
    WHERE
        [OrganizationUserId] = @Id

    DELETE
    FROM
        [dbo].[GroupUser]
    WHERE
        [OrganizationUserId] = @Id

    EXEC [dbo].[OrganizationSponsorship_OrganizationUserDeleted] @Id

    DELETE
    FROM
        [dbo].[OrganizationUser]
    WHERE
        [Id] = @Id
END
GO


IF OBJECT_ID('[dbo].[OrganizationUser_DeleteByIds]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationUser_DeleteByIds]
END
GO

CREATE PROCEDURE [dbo].[OrganizationUser_DeleteByIds]
    @Ids [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationUserIds] @Ids

    DECLARE @UserAndOrganizationIds [dbo].[TwoGuidIdArray]

    INSERT INTO @UserAndOrganizationIds
        (Id1, Id2)
    SELECT
        UserId,
        OrganizationId
    FROM
        [dbo].[OrganizationUser] OU
    INNER JOIN
        @Ids OUIds ON OUIds.Id = OU.Id
    WHERE
        UserId IS NOT NULL AND
        OrganizationId IS NOT NULL

    BEGIN
        EXEC [dbo].[SsoUser_DeleteMany] @UserAndOrganizationIds
    END

    DECLARE @BatchSize INT = 100

    -- Delete CollectionUsers
    WHILE @BatchSize > 0
    BEGIN
        BEGIN TRANSACTION CollectionUser_DeleteMany_CUs

        DELETE TOP(@BatchSize) CU
        FROM
            [dbo].[CollectionUser] CU
        INNER JOIN
            @Ids I ON I.Id = CU.OrganizationUserId

        SET @BatchSize = @@ROWCOUNT

        COMMIT TRANSACTION CollectionUser_DeleteMany_CUs
    END

    SET @BatchSize = 100;

    -- Delete GroupUsers
    WHILE @BatchSize > 0
    BEGIN
        BEGIN TRANSACTION GroupUser_DeleteMany_GroupUsers

        DELETE TOP(@BatchSize) GU
        FROM
            [dbo].[GroupUser] GU
        INNER JOIN
            @Ids I ON I.Id = GU.OrganizationUserId

        SET @BatchSize = @@ROWCOUNT

        COMMIT TRANSACTION GoupUser_DeleteMany_GroupUsers
    END

    EXEC [dbo].[OrganizationSponsorship_OrganizationUsersDeleted] @Ids
    
    SET @BatchSize = 100;

    -- Delete OrganizationUsers
    WHILE @BatchSize > 0
        BEGIN
        BEGIN TRANSACTION OrganizationUser_DeleteMany_OUs

        DELETE TOP(@BatchSize) OU
        FROM
            [dbo].[OrganizationUser] OU
        INNER JOIN
            @Ids I ON I.Id = OU.Id

        SET @BatchSize = @@ROWCOUNT

        COMMIT TRANSACTION OrganizationUser_DeleteMany_OUs
    END
END
GO

-- OrganizationUserOrganizationDetailsView update
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
    O.[UseGroups],
    O.[UseDirectory],
    O.[UseEvents],
    O.[UseTotp],
    O.[Use2fa],
    O.[UseApi],
    O.[UseResetPassword],
    O.[SelfHost],
    O.[UsersGetPremium],
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
    OS.[FriendlyName] FamilySponsorshipFriendlyName
FROM
    [dbo].[OrganizationUser] OU
INNER JOIN
    [dbo].[Organization] O ON O.[Id] = OU.[OrganizationId]
LEFT JOIN
    [dbo].[SsoUser] SU ON SU.[UserId] = OU.[UserId] AND SU.[OrganizationId] = OU.[OrganizationId]
LEFT JOIN
    [dbo].[ProviderOrganization] PO ON PO.[OrganizationId] = O.[Id]
LEFT JOIN
    [dbo].[Provider] P ON P.[Id] = PO.[ProviderId]
LEFT JOIN
    [dbo].[OrganizationSponsorship] OS ON OS.[SponsoringOrganizationUserId] = OU.[Id]
