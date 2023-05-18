-- Update views
CREATE OR ALTER VIEW [dbo].[ApiKeyDetailsView]
AS
SELECT
    AK.[Id],
    AK.[ServiceAccountId],
    AK.[Name],
    AK.[HashedClientSecret],
    AK.[Scope],
    AK.[EncryptedPayload],
    AK.[Key],
    AK.[ExpireAt],
    AK.[CreationDate],
    AK.[RevisionDate],
    SA.[OrganizationId] ServiceAccountOrganizationId
FROM
    [dbo].[ApiKey] AS AK
LEFT JOIN
    [dbo].[ServiceAccount] SA ON SA.[Id] = AK.[ServiceAccountId]
GO

CREATE OR ALTER VIEW [dbo].[ApiKeyView]
AS
SELECT [Id],
    [ServiceAccountId],
    [Name],
    [HashedClientSecret],
    [Scope],
    [EncryptedPayload],
    [Key],
    [ExpireAt],
    [CreationDate],
    [RevisionDate]
FROM [dbo].[ApiKey]
GO

-- Remove Column
IF COL_LENGTH('[dbo].[ApiKey]', 'ClientSecret') IS NOT NULL
BEGIN
    ALTER TABLE
        [dbo].[ApiKey]
    DROP COLUMN
        [ClientSecret]
END
GO

-- Drop existing SPROC
IF OBJECT_ID('[dbo].[ApiKey_Create]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[ApiKey_Create]
END
GO

-- Create the new SPROC
CREATE PROCEDURE [dbo].[ApiKey_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @ServiceAccountId UNIQUEIDENTIFIER,
    @Name VARCHAR(200),
    @HashedClientSecret VARCHAR(128),
    @Scope NVARCHAR(4000),
    @EncryptedPayload NVARCHAR(4000),
    @Key VARCHAR(MAX),
    @ExpireAt DATETIME2(7),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[ApiKey] 
    (
        [Id],
        [ServiceAccountId],
        [Name],
        [HashedClientSecret],
        [Scope],
        [EncryptedPayload],
        [Key],
        [ExpireAt],
        [CreationDate],
        [RevisionDate]
    )
    VALUES 
    (
        @Id,
        @ServiceAccountId,
        @Name,
        @HashedClientSecret,
        @Scope,
        @EncryptedPayload,
        @Key,
        @ExpireAt,
        @CreationDate,
        @RevisionDate
    )
END
