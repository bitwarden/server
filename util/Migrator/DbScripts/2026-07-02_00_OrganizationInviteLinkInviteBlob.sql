-- Replace EncryptedInviteKey/EncryptedOrgKey with an Invite blob column and a SupportsConfirmation flag.
-- Mirrors the EF migration (drop EncryptedOrgKey, rename EncryptedInviteKey -> Invite, add SupportsConfirmation
-- at the end) so the resulting column order stays consistent across all supported databases.

-- Drop the org key column
IF COL_LENGTH('[dbo].[OrganizationInviteLink]', 'EncryptedOrgKey') IS NOT NULL
BEGIN
    ALTER TABLE [dbo].[OrganizationInviteLink]
        DROP COLUMN [EncryptedOrgKey];
END
GO

-- Rename EncryptedInviteKey -> Invite (preserves the column's position, NOT NULL-ness, and data)
IF COL_LENGTH('[dbo].[OrganizationInviteLink]', 'EncryptedInviteKey') IS NOT NULL
    AND COL_LENGTH('[dbo].[OrganizationInviteLink]', 'Invite') IS NULL
BEGIN
    EXEC sp_rename '[dbo].[OrganizationInviteLink].[EncryptedInviteKey]', 'Invite', 'COLUMN';
END
GO

-- Add the SupportsConfirmation flag at the end
IF COL_LENGTH('[dbo].[OrganizationInviteLink]', 'SupportsConfirmation') IS NULL
BEGIN
    ALTER TABLE [dbo].[OrganizationInviteLink]
        ADD [SupportsConfirmation] BIT NOT NULL CONSTRAINT [DF_OrganizationInviteLink_SupportsConfirmation] DEFAULT (0);

    ALTER TABLE [dbo].[OrganizationInviteLink]
        DROP CONSTRAINT [DF_OrganizationInviteLink_SupportsConfirmation];
END
GO

-- Refresh the view's cached metadata to pick up the table's column changes (definition is unchanged)
EXECUTE sp_refreshview N'[dbo].[OrganizationInviteLinkView]';
GO

-- Stored Procedures
CREATE OR ALTER PROCEDURE [dbo].[OrganizationInviteLink_Create]
    @Id                   UNIQUEIDENTIFIER OUTPUT,
    @Code                 UNIQUEIDENTIFIER,
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
    @Code                 UNIQUEIDENTIFIER,
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

-- Refresh read sprocs (SELECT * through the view) so their cached metadata picks up the column changes
EXECUTE sp_refreshsqlmodule N'[dbo].[OrganizationInviteLink_ReadById]';
EXECUTE sp_refreshsqlmodule N'[dbo].[OrganizationInviteLink_ReadByCode]';
EXECUTE sp_refreshsqlmodule N'[dbo].[OrganizationInviteLink_ReadByOrganizationId]';
GO
