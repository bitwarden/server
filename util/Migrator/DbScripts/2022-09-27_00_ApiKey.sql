IF OBJECT_ID('[dbo].[ApiKey]') IS NULL
BEGIN
    CREATE TABLE [dbo].[ApiKey] (
        [Id]               UNIQUEIDENTIFIER,
        [ServiceAccountId] UNIQUEIDENTIFIER NULL,
        [Name]             VARCHAR(200) NOT NULL,
        [ClientSecret]     VARCHAR(30) NOT NULL,
        [Scope]            NVARCHAR (4000) NOT NULL,
        [EncryptedPayload] NVARCHAR (4000) NOT NULL,
        [Key]              VARCHAR (MAX) NOT NULL,
        [ExpireAt]         DATETIME2(7) NOT NULL,
        [CreationDate]     DATETIME2(7) NOT NULL,
        [RevisionDate]     DATETIME2(7) NOT NULL,
        CONSTRAINT [PK_ApiKey] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_ApiKey_ServiceAccountId] FOREIGN KEY ([ServiceAccountId]) REFERENCES [dbo].[ServiceAccount] ([Id])
    );

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
