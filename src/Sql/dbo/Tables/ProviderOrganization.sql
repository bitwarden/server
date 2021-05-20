CREATE TABLE [dbo].[ProviderOrganization] (
    [Id]             UNIQUEIDENTIFIER    NOT NULL,
    [ProviderId]     UNIQUEIDENTIFIER    NOT NULL,
    [OrganizationId] UNIQUEIDENTIFIER    NULL,
    [Key]            VARCHAR (MAX)       NULL,
    [Settings]       NVARCHAR(MAX)       NULL,
    [CreationDate]   DATETIME2 (7)       NOT NULL,
    [RevisionDate]   DATETIME2 (7)       NOT NULL,
    CONSTRAINT [PK_ProviderOrganization] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_ProviderOrganization_Provider] FOREIGN KEY ([ProviderId]) REFERENCES [dbo].[Provider] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ProviderOrganization_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id])
);
