CREATE TABLE [dbo].[ProviderOrganizationProviderUser] (
    [Id]                     UNIQUEIDENTIFIER    NOT NULL,
    [ProviderOrganizationId] UNIQUEIDENTIFIER    NOT NULL,
    [ProviderUserId]         UNIQUEIDENTIFIER    NULL,
    [Type]                   TINYINT             NOT NULL,
    [CreationDate]           DATETIME2 (7)       NOT NULL,
    [RevisionDate]           DATETIME2 (7)       NOT NULL,
    CONSTRAINT [PK_ProviderOrganizationProviderUser] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_ProviderOrganizationProviderUser_Provider] FOREIGN KEY ([ProviderOrganizationId]) REFERENCES [dbo].[ProviderOrganization] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ProviderOrganizationProviderUser_User] FOREIGN KEY ([ProviderUserId]) REFERENCES [dbo].[ProviderUser] ([Id])
);
