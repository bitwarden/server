CREATE PROCEDURE [dbo].[OpaqueKeyExchangeCredential_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @UserId UNIQUEIDENTIFIER,
    @CipherConfiguration VARCHAR(MAX) NOT NULL,
    @CredentialBlob VARCHAR(MAX) NOT NULL,
    @EncryptedPublicKey VARCHAR(MAX) NOT NULL,
    @EncryptedPrivateKey VARCHAR(MAX) NOT NULL,
    @EncryptedUserKey VARCHAR(MAX) NOT NULL,
    @CreationDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[OpaqueKeyExchangeCredential]
        (
        [Id],
        [UserId],
        [CipherConfiguration],
        [CredentialBlob],
        [EncryptedPublicKey],
        [EncryptedPrivateKey],
        [EncryptedUserKey],
        [CreationDate]
        )
    VALUES
        (
            @Id,
            @UserId,
            @CipherConfiguration,
            @CredentialBlob,
            @EncryptedPublicKey,
            @EncryptedPrivateKey,
            @EncryptedUserKey,
            @CreationDate
    )
END
