-- Remove Column
IF COL_LENGTH('[dbo].[ApiKey]', 'ClientSecret') IS NOT NULL
BEGIN
    ALTER TABLE
        [dbo].[ApiKey]
    DROP COLUMN
        [ClientSecret]
END
GO

-- Refresh views
IF OBJECT_ID('[dbo].[ApiKeyDetailsView]') IS NOT NULL
    BEGIN
        EXECUTE sp_refreshview N'[dbo].[ApiKeyDetailsView]';
    END
GO

IF OBJECT_ID('[dbo].[ApiKeyView]') IS NOT NULL
    BEGIN
        EXECUTE sp_refreshview N'[dbo].[ApiKeyView]';
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
