CREATE TABLE [dbo].[OrganizationInstallation] (
    [Id]             UNIQUEIDENTIFIER NOT NULL,
    [OrganizationId] UNIQUEIDENTIFIER NOT NULL,
    [InstallationId] UNIQUEIDENTIFIER NOT NULL,
    [CreationDate]   DATETIME2 (7) NOT NULL,
    [RevisionDate]   DATETIME2 (7) NULL,
    CONSTRAINT [PK_OrganizationInstallation] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_OrganizationInstallation_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_OrganizationInstallation_Installation] FOREIGN KEY ([InstallationId]) REFERENCES [dbo].[Installation] ([Id]) ON DELETE CASCADE
);
GO

CREATE NONCLUSTERED INDEX [IX_OrganizationInstallation_OrganizationId]
    ON [dbo].[OrganizationInstallation]([OrganizationId] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_OrganizationInstallation_InstallationId]
    ON [dbo].[OrganizationInstallation]([InstallationId] ASC);
