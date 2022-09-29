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

    CREATE NONCLUSTERED INDEX [IX_ApiKey_UserId]
        ON [dbo].[ApiKey]([UserId] ASC);

    CREATE NONCLUSTERED INDEX [IX_ApiKey_OrganizationId]
        ON [dbo].[ApiKey]([OrganizationId] ASC);

    CREATE NONCLUSTERED INDEX [IX_ApiKey_ServiceAccountId]
        ON [dbo].[ApiKey]([ServiceAccountId] ASC);
END
GO

CREATE OR ALTER VIEW [dbo].[ApiKeyView]
AS
SELECT
    *
FROM
    [dbo].[ApiKey]
GO

CREATE OR ALTER PROCEDURE [dbo].[ApiKey_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[ApiKeyView]
    WHERE
        [Id] = @Id
END
