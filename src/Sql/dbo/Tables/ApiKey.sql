CREATE TABLE [dbo].[ApiKey] (
    [Id]               UNIQUEIDENTIFIER,
    [UserId]           UNIQUEIDENTIFIER NULL,
    [OrganizationId]   UNIQUEIDENTIFIER NULL,
    [ServiceAccountId] UNIQUEIDENTIFIER NULL,
    [Name]             VARCHAR(200) NOT NULL,
    [ClientSecret]     VARCHAR(30) NOT NULL,
    [Scope]            NVARCHAR (4000) NOT NULL,
    [EncryptedPayload] NVARCHAR (4000) NOT NULL,
    [ExpireAt]         DATETIME2(7) NOT NULL,
    [CreationDate]     DATETIME2(7) NOT NULL,
    [RevisionDate]     DATETIME2(7) NOT NULL,
    CONSTRAINT [PK_ApiKey] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_ApiKey_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id]),
    CONSTRAINT [FK_ApiKey_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]),
    CONSTRAINT [FK_ApiKey_ServiceAccountId] FOREIGN KEY ([ServiceAccountId]) REFERENCES [dbo].[ServiceAccount] ([Id])
);

GO
CREATE NONCLUSTERED INDEX [IX_ApiKey_UserId]
    ON [dbo].[ApiKey]([UserId] ASC);

GO
CREATE NONCLUSTERED INDEX [IX_ApiKey_OrganizationId]
    ON [dbo].[ApiKey]([OrganizationId] ASC);

GO
CREATE NONCLUSTERED INDEX [IX_ApiKey_ServiceAccountId]
    ON [dbo].[ApiKey]([ServiceAccountId] ASC);
