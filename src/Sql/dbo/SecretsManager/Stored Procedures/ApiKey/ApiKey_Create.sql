CREATE PROCEDURE [dbo].[ApiKey_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @ServiceAccountId UNIQUEIDENTIFIER,
    @Name VARCHAR(200),
    @ClientSecretHash VARCHAR(128),
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
        [ClientSecretHash],
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
        @ClientSecretHash,
        @Scope,
        @EncryptedPayload,
        @Key,
        @ExpireAt,
        @CreationDate,
        @RevisionDate
    )
END
