CREATE TABLE [dbo].[ApiKey] (
    [Id]                    UNIQUEIDENTIFIER,
    [ServiceAccountId]      UNIQUEIDENTIFIER NULL,
    [Name]                  NVARCHAR(200) NOT NULL,
    [ClientSecretHash]      VARCHAR(128) NULL,
    [Scope]                 NVARCHAR (4000) NOT NULL,
    [EncryptedPayload]      NVARCHAR (4000) NULL,
    [Key]                   VARCHAR (MAX) NULL,
    [ExpireAt]              DATETIME2(7) NULL,
    [CreationDate]          DATETIME2(7) NOT NULL,
    [RevisionDate]          DATETIME2(7) NOT NULL,
    [OrganizationId]        UNIQUEIDENTIFIER NULL,
    CONSTRAINT [PK_ApiKey] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_ApiKey_ServiceAccountId] FOREIGN KEY ([ServiceAccountId]) REFERENCES [dbo].[ServiceAccount] ([Id]),
    CONSTRAINT [FK_ApiKey_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]),
    CONSTRAINT [CK_ApiKey_SingleOwner] CHECK ([ServiceAccountId] IS NULL OR [OrganizationId] IS NULL)
);

GO
CREATE NONCLUSTERED INDEX [IX_ApiKey_ServiceAccountId]
    ON [dbo].[ApiKey]([ServiceAccountId] ASC);

GO
CREATE NONCLUSTERED INDEX [IX_ApiKey_OrganizationId]
    ON [dbo].[ApiKey]([OrganizationId] ASC);
