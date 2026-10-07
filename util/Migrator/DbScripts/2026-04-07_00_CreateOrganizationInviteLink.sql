-- Table
IF OBJECT_ID('[dbo].[OrganizationInviteLink]') IS NULL
BEGIN
    CREATE TABLE [dbo].[OrganizationInviteLink]
    (
        [Id]                 UNIQUEIDENTIFIER NOT NULL,
        [Code]               UNIQUEIDENTIFIER NOT NULL,
        [OrganizationId]     UNIQUEIDENTIFIER NOT NULL,
        [AllowedDomains]     NVARCHAR(MAX)    NOT NULL,
        [EncryptedInviteKey] NVARCHAR(MAX)    NOT NULL,
        [EncryptedOrgKey]    NVARCHAR(MAX)    NULL,
        [CreationDate]       DATETIME2(7)     NOT NULL,
        [RevisionDate]       DATETIME2(7)     NOT NULL,
        CONSTRAINT [PK_OrganizationInviteLink] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_OrganizationInviteLink_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]) ON DELETE CASCADE
    );

    CREATE UNIQUE NONCLUSTERED INDEX [IX_OrganizationInviteLink_OrganizationId]
        ON [dbo].[OrganizationInviteLink]([OrganizationId] ASC);

    CREATE UNIQUE NONCLUSTERED INDEX [IX_OrganizationInviteLink_Code]
        ON [dbo].[OrganizationInviteLink]([Code] ASC);
END
GO

-- View
CREATE OR ALTER VIEW [dbo].[OrganizationInviteLinkView]
AS
SELECT
    *
FROM
    [dbo].[OrganizationInviteLink]
GO

-- Stored Procedures
CREATE OR ALTER PROCEDURE [dbo].[OrganizationInviteLink_Create]
    @Id                 UNIQUEIDENTIFIER OUTPUT,
    @Code               UNIQUEIDENTIFIER,
    @OrganizationId     UNIQUEIDENTIFIER,
    @AllowedDomains     NVARCHAR(MAX),
    @EncryptedInviteKey NVARCHAR(MAX),
    @EncryptedOrgKey    NVARCHAR(MAX),
    @CreationDate       DATETIME2(7),
    @RevisionDate       DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[OrganizationInviteLink]
    (
        [Id],
        [Code],
        [OrganizationId],
        [AllowedDomains],
        [EncryptedInviteKey],
        [EncryptedOrgKey],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @Code,
        @OrganizationId,
        @AllowedDomains,
        @EncryptedInviteKey,
        @EncryptedOrgKey,
        @CreationDate,
        @RevisionDate
    )
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationInviteLink_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationInviteLinkView]
    WHERE
        [Id] = @Id
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationInviteLink_Update]
    @Id                 UNIQUEIDENTIFIER,
    @Code               UNIQUEIDENTIFIER,
    @OrganizationId     UNIQUEIDENTIFIER,
    @AllowedDomains     NVARCHAR(MAX),
    @EncryptedInviteKey NVARCHAR(MAX),
    @EncryptedOrgKey    NVARCHAR(MAX),
    @CreationDate       DATETIME2(7),
    @RevisionDate       DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[OrganizationInviteLink]
    SET
        [Code] = @Code,
        [OrganizationId] = @OrganizationId,
        [AllowedDomains] = @AllowedDomains,
        [EncryptedInviteKey] = @EncryptedInviteKey,
        [EncryptedOrgKey] = @EncryptedOrgKey,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationInviteLink_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE FROM [dbo].[OrganizationInviteLink]
    WHERE
        [Id] = @Id
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationInviteLink_ReadByCode]
    @Code UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationInviteLinkView]
    WHERE
        [Code] = @Code
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationInviteLink_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationInviteLinkView]
    WHERE
        [OrganizationId] = @OrganizationId
END
GO
