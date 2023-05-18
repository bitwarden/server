CREATE TABLE [dbo].[ApiKey] (
    [Id]               UNIQUEIDENTIFIER,
    [ServiceAccountId] UNIQUEIDENTIFIER NULL,
    [Name]             VARCHAR(200) NOT NULL,
    [HashedClientSecret]    VARCHAR(128) NULL,
    [Scope]            NVARCHAR (4000) NOT NULL,
    [EncryptedPayload] NVARCHAR (4000) NOT NULL,
    [Key]              VARCHAR (MAX) NOT NULL,
    [ExpireAt]         DATETIME2(7) NULL,
    [CreationDate]     DATETIME2(7) NOT NULL,
    [RevisionDate]     DATETIME2(7) NOT NULL,
    CONSTRAINT [PK_ApiKey] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_ApiKey_ServiceAccountId] FOREIGN KEY ([ServiceAccountId]) REFERENCES [dbo].[ServiceAccount] ([Id])
);

GO
CREATE NONCLUSTERED INDEX [IX_ApiKey_ServiceAccountId]
    ON [dbo].[ApiKey]([ServiceAccountId] ASC);
