-- Data-protect OrganizationInviteLink.Code at rest: change column from UNIQUEIDENTIFIER to
-- NVARCHAR(300), drop the Code unique index and ReadByCode sproc, refresh view and sproc metadata.
-- COORDINATED DEPLOY: run only after all server instances are on the new version.

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OrganizationInviteLink_Code' AND object_id = OBJECT_ID('[dbo].[OrganizationInviteLink]'))
BEGIN
    DROP INDEX [IX_OrganizationInviteLink_Code] ON [dbo].[OrganizationInviteLink];
END
GO

IF EXISTS (
    SELECT *
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'OrganizationInviteLink'
        AND COLUMN_NAME = 'Code'
        AND DATA_TYPE = 'uniqueidentifier')
BEGIN
    ALTER TABLE [dbo].[OrganizationInviteLink]
        ALTER COLUMN [Code] NVARCHAR(300) NOT NULL;
END
GO

EXECUTE sp_refreshview N'[dbo].[OrganizationInviteLinkView]';
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationInviteLink_Create]
    @Id                   UNIQUEIDENTIFIER OUTPUT,
    @Code                 NVARCHAR(300),
    @OrganizationId       UNIQUEIDENTIFIER,
    @AllowedDomains       NVARCHAR(MAX),
    @Invite               NVARCHAR(MAX),
    @SupportsConfirmation BIT,
    @CreationDate         DATETIME2(7),
    @RevisionDate         DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[OrganizationInviteLink]
    (
        [Id],
        [Code],
        [OrganizationId],
        [AllowedDomains],
        [Invite],
        [SupportsConfirmation],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @Code,
        @OrganizationId,
        @AllowedDomains,
        @Invite,
        @SupportsConfirmation,
        @CreationDate,
        @RevisionDate
    )
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationInviteLink_Update]
    @Id                   UNIQUEIDENTIFIER,
    @Code                 NVARCHAR(300),
    @OrganizationId       UNIQUEIDENTIFIER,
    @AllowedDomains       NVARCHAR(MAX),
    @Invite               NVARCHAR(MAX),
    @SupportsConfirmation BIT,
    @CreationDate         DATETIME2(7),
    @RevisionDate         DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[OrganizationInviteLink]
    SET
        [Code] = @Code,
        [OrganizationId] = @OrganizationId,
        [AllowedDomains] = @AllowedDomains,
        [Invite] = @Invite,
        [SupportsConfirmation] = @SupportsConfirmation,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END
GO

DROP PROCEDURE IF EXISTS [dbo].[OrganizationInviteLink_ReadByCode];
GO

IF OBJECT_ID('[dbo].[OrganizationInviteLink_ReadById]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[OrganizationInviteLink_ReadById]';
END
GO

IF OBJECT_ID('[dbo].[OrganizationInviteLink_ReadByOrganizationId]') IS NOT NULL
BEGIN
    EXECUTE sp_refreshsqlmodule N'[dbo].[OrganizationInviteLink_ReadByOrganizationId]';
END
GO
