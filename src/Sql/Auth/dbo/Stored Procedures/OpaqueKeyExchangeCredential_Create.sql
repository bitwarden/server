CREATE PROCEDURE [dbo].[OpaqueKeyExchangeCredential_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @UserId UNIQUEIDENTIFIER,
    @CipherConfiguration VARCHAR(MAX),
    @CredentialBlob VARCHAR(MAX),
    @EncryptedPublicKey VARCHAR(MAX),
    @EncryptedPrivateKey VARCHAR(MAX),
    @EncryptedUserKey VARCHAR(MAX),
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
