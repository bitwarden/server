CREATE PROCEDURE [dbo].[WebAuthnCredential_Update]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @Name NVARCHAR(50),
    @PublicKey VARCHAR (256),
    @CredentialId VARCHAR(256),
    @Counter INT,
    @Type VARCHAR(20),
    @AaGuid UNIQUEIDENTIFIER,
    @EncryptedUserKey VARCHAR (MAX),
    @EncryptedPrivateKey VARCHAR (MAX),
    @EncryptedPublicKey VARCHAR (MAX),
    @SupportsPrf BIT,
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[WebAuthnCredential]
    SET
        [UserId] = @UserId,
        [Name] = @Name,
        [PublicKey] = @PublicKey,
        [CredentialId] = @CredentialId,
        [Counter] = @Counter,
        [Type] = @Type,
        [AaGuid] = @AaGuid,
        [EncryptedUserKey] = @EncryptedUserKey,
        [EncryptedPrivateKey] = @EncryptedPrivateKey,
        [EncryptedPublicKey] = @EncryptedPublicKey,
        [SupportsPrf] = @SupportsPrf,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END
