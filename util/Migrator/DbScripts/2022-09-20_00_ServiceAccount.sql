IF OBJECT_ID('[dbo].[ServiceAccount]') IS NULL
BEGIN

    CREATE TABLE [dbo].[ServiceAccount]
    (
        [Id]             UNIQUEIDENTIFIER NOT NULL,
        [OrganizationId] UNIQUEIDENTIFIER NOT NULL,
        [Name]           NVARCHAR(MAX) NULL,
        [CreationDate]   DATETIME2(7) NOT NULL,
        [RevisionDate]   DATETIME2(7) NOT NULL,
        [DeletedDate]    DATETIME2(7) NULL,
        CONSTRAINT [PK_ServiceAccount] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_ServiceAccount_OrganizationId] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization]([Id]) ON DELETE CASCADE
    );
END
GO

IF OBJECT_ID('[dbo].[ApiKey]') IS NULL
BEGIN
    CREATE TABLE [dbo].[ApiKey] (
        [Id]               UNIQUEIDENTIFIER,
        [UserId]           UNIQUEIDENTIFIER NULL,
        [OrganizationId]   UNIQUEIDENTIFIER NULL,
        [ServiceAccountId] UNIQUEIDENTIFIER NULL,
        [ClientSecret]     VARCHAR(30) NOT NULL,
        [Scope]            NVARCHAR (MAX) NOT NULL,
        [EncryptedPayload] NVARCHAR (MAX) NOT NULL,
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
END
GO
