CREATE PROCEDURE [dbo].[WebAuthnCredential_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
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

    INSERT INTO [dbo].[WebAuthnCredential]
    (
        [Id],
        [UserId],
        [Name],
        [PublicKey],
        [CredentialId],
        [Counter],
        [Type],
        [AaGuid],
        [EncryptedUserKey],
        [EncryptedPrivateKey],
        [EncryptedPublicKey],
        [SupportsPrf],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @UserId,
        @Name,
        @PublicKey,
        @CredentialId,
        @Counter,
        @Type,
        @AaGuid,
        @EncryptedUserKey,
        @EncryptedPrivateKey,
        @EncryptedPublicKey,
        @SupportsPrf,
        @CreationDate,
        @RevisionDate
    )
END
