CREATE PROCEDURE [dbo].[Device_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @UserId UNIQUEIDENTIFIER,
    @Name NVARCHAR(50),
    @Type TINYINT,
    @Identifier NVARCHAR(50),
    @PushToken NVARCHAR(255),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @PublicKeyEncryptedSymmetricKey VARCHAR(MAX) = NULL,
    @EncryptionKeyEncryptedPublicKey VARCHAR(MAX) = NULL,
    @DeviceKeyEncryptedPrivateKey VARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[Device]
    (
        [Id],
        [UserId],
        [Name],
        [Type],
        [Identifier],
        [PushToken],
        [CreationDate],
        [RevisionDate],
        [PublicKeyEncryptedSymmetricKey],
        [EncryptionKeyEncryptedPublicKey],
        [DeviceKeyEncryptedPrivateKey]
    )
    VALUES
    (
        @Id,
        @UserId,
        @Name,
        @Type,
        @Identifier,
        @PushToken,
        @CreationDate,
        @RevisionDate,
        @PublicKeyEncryptedSymmetricKey,
        @EncryptionKeyEncryptedPublicKey,
        @DeviceKeyEncryptedPrivateKey
    )
END
