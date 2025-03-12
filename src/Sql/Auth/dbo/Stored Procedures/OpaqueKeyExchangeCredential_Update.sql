-- Used for Key Rotation and Password Update
CREATE PROCEDURE [dbo].[OpaqueKeyExchangeCredential_Update]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @UserId UNIQUEIDENTIFIER,
    @CipherConfiguration UNIQUEIDENTIFIER = NULL,
    @CredentialBlob TINYINT,
    @EncryptedPublicKey NVARCHAR(50),
    @EncryptedPrivateKey TINYINT,
    @EncryptedUserKey VARCHAR(50),
    @CreationDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
    [dbo].[OpaqueKeyExchangeCredential]
SET
        [CipherConfiguration] = @CipherConfiguration,
        [CredentialBlob] = @CredentialBlob,
        [EncryptedPublicKey] = @EncryptedPublicKey,
        [EncryptedPrivateKey] = @EncryptedPrivateKey,
        [EncryptedUserKey] = @EncryptedUserKey,
        [CreationDate] = @CreationDate
WHERE
    [Id] = @Id AND [UserId] = @UserId
END
