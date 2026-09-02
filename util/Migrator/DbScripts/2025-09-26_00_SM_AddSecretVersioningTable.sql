SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'SecretVersion' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE [dbo].[SecretVersion] (
        [Id]                       UNIQUEIDENTIFIER NOT NULL,
        [SecretId]                 UNIQUEIDENTIFIER NOT NULL,
        [Value]                    NVARCHAR (MAX)   NOT NULL,
        [VersionDate]              DATETIME2 (7)    NOT NULL,
        [EditorServiceAccountId]   UNIQUEIDENTIFIER NULL,
        [EditorOrganizationUserId] UNIQUEIDENTIFIER NULL,
        CONSTRAINT [PK_SecretVersion] PRIMARY KEY CLUSTERED ([Id] ASC)
    );
END
GO

-- Ensure foreign keys exist
IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_SecretVersion_OrganizationUser'
)
BEGIN
    ALTER TABLE [dbo].[SecretVersion]
    ADD CONSTRAINT [FK_SecretVersion_OrganizationUser]
        FOREIGN KEY ([EditorOrganizationUserId])
        REFERENCES [dbo].[OrganizationUser] ([Id])
        ON DELETE SET NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_SecretVersion_Secret'
)
BEGIN
    ALTER TABLE [dbo].[SecretVersion]
    ADD CONSTRAINT [FK_SecretVersion_Secret]
        FOREIGN KEY ([SecretId])
        REFERENCES [dbo].[Secret] ([Id])
        ON DELETE CASCADE;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_SecretVersion_ServiceAccount'
)
BEGIN
    ALTER TABLE [dbo].[SecretVersion]
    ADD CONSTRAINT [FK_SecretVersion_ServiceAccount]
        FOREIGN KEY ([EditorServiceAccountId])
        REFERENCES [dbo].[ServiceAccount] ([Id])
        ON DELETE SET NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes WHERE name = 'IX_SecretVersion_SecretId' AND object_id = OBJECT_ID('[dbo].[SecretVersion]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_SecretVersion_SecretId]
        ON [dbo].[SecretVersion]([SecretId] ASC);
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes WHERE name = 'IX_SecretVersion_EditorServiceAccountId' AND object_id = OBJECT_ID('[dbo].[SecretVersion]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_SecretVersion_EditorServiceAccountId]
        ON [dbo].[SecretVersion]([EditorServiceAccountId] ASC)
        WHERE [EditorServiceAccountId] IS NOT NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes WHERE name = 'IX_SecretVersion_EditorOrganizationUserId' AND object_id = OBJECT_ID('[dbo].[SecretVersion]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_SecretVersion_EditorOrganizationUserId]
        ON [dbo].[SecretVersion]([EditorOrganizationUserId] ASC)
        WHERE [EditorOrganizationUserId] IS NOT NULL;
END
GO
