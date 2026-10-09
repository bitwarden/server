CREATE TABLE [dbo].[OrganizationScopedApiKey]
(
    [Id]               UNIQUEIDENTIFIER NOT NULL,
    [OrganizationId]   UNIQUEIDENTIFIER NOT NULL,
    [Name]             NVARCHAR(200)    NOT NULL,
    [ClientSecretHash] VARCHAR(128)     NOT NULL,
    [Scopes]           NVARCHAR(4000)   NOT NULL,
    [ExpireAt]         DATETIME2(7)     NULL,
    [CreationDate]     DATETIME2(7)     NOT NULL,
    [RevisionDate]     DATETIME2(7)     NOT NULL,
    CONSTRAINT [PK_OrganizationScopedApiKey] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_OrganizationScopedApiKey_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id])
);
GO

CREATE NONCLUSTERED INDEX [IX_OrganizationScopedApiKey_OrganizationId]
    ON [dbo].[OrganizationScopedApiKey]([OrganizationId] ASC);
GO
