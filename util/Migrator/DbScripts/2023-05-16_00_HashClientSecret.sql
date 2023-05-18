IF COL_LENGTH('[dbo].[ApiKey]', 'HashedClientSecret') IS NULL
BEGIN
  ALTER TABLE [dbo].[ApiKey]
  ADD [HashedClientSecret] VARCHAR(128);
END
GO

-- Refresh views
CREATE OR ALTER VIEW [dbo].[ApiKeyDetailsView]
AS
SELECT
    AK.[Id],
    AK.[ServiceAccountId],
    AK.[Name],
    AK.[ClientSecret],
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
    [ClientSecret],
    [HashedClientSecret],
    [Scope],
    [EncryptedPayload],
    [Key],
    [ExpireAt],
    [CreationDate],
    [RevisionDate]
FROM [dbo].[ApiKey]
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
    @ClientSecret VARCHAR(30) = 'migrated', -- Deprecated as of 2023-05-17
    @HashedClientSecret VARCHAR(128) = NULL,
    @Scope NVARCHAR(4000),
    @EncryptedPayload NVARCHAR(4000),
    @Key VARCHAR(MAX),
    @ExpireAt DATETIME2(7),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    IF (@HashedClientSecret IS NULL)
    BEGIN
      DECLARE @hb VARBINARY(128) = HASHBYTES('SHA2_256', @ClientSecret);
      SET @HashedClientSecret = CAST(N'' as xml).value('xs:base64Binary(sql:variable("@hb"))', 'VARCHAR(128)');
    END

    INSERT INTO [dbo].[ApiKey] 
    (
        [Id],
        [ServiceAccountId],
        [Name],
        [ClientSecret],
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
        @ClientSecret,
        @HashedClientSecret,
        @Scope,
        @EncryptedPayload,
        @Key,
        @ExpireAt,
        @CreationDate,
        @RevisionDate
    )
END
GO

-- Data Migration
UPDATE ApiKey
SET HashedClientSecret = (
    SELECT CAST(N'' AS XML).value('xs:base64Binary(sql:column("HASH"))', 'VARCHAR(128)')
    FROM (
      SELECT HASHBYTES('SHA2_256', ClientSecret) AS HASH
      ) SRC
    )
WHERE HashedClientSecret IS NULL
