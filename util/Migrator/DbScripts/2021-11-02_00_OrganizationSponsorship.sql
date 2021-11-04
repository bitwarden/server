-- Create Organization Sponsorships table
IF OBJECT_ID('[dbo].[OrganizationSponsorship]') IS NULL
BEGIN
CREATE TABLE [dbo].[OrganizationSponsorship] (
    [Id]                            UNIQUEIDENTIFIER NOT NULL,
    [InstallationId]                UNIQUEIDENTIFIER NULL,
    [SponsoringOrganizationId]      UNIQUEIDENTIFIER NOT NULL,
    [SponsoringOrganizationUserID]  UNIQUEIDENTIFIER NOT NULL,
    [SponsoredOrganizationId]       UNIQUEIDENTIFIER NULL,
    [OfferedToEmail]                NVARCHAR (256)   NULL,
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
END
GO

IF NOT EXISTS(SELECT name FROM sys.indexes WHERE name = 'IX_OrganizationSponsorship_SponsoringOrganizationUserId')
BEGIN
CREATE NONCLUSTERED INDEX [IX_OrganizationSponsorship_SponsoringOrganizationUserId]
    ON [dbo].[OrganizationSponsorship]([SponsoringOrganizationUserID] ASC)
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
    @OfferedToEmail NVARCHAR(256),
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
        [OfferedToEmail],
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
        @OfferedToEmail,
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
    @OfferedToEmail NVARCHAR(256),
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
        [OfferedToEmail] = @OfferedToEmail,
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
