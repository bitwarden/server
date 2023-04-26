CREATE PROCEDURE [dbo].[WebAuthnCredential_Update]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @Name NVARCHAR(50),
    @PublicKey VARCHAR (256),
    @DescriptorId VARCHAR(256),
    @Counter INT,
    @Type VARCHAR(20),
    @AaGuid UNIQUEIDENTIFIER,
    @UserKey VARCHAR (MAX),
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
        [DescriptorId] = @DescriptorId,
        [Counter] = @Counter,
        [Type] = @Type,
        [AaGuid] = @AaGuid,
        [UserKey] = @UserKey,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END