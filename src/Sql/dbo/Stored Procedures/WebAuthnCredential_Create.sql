CREATE PROCEDURE [dbo].[WebAuthnCredential_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
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

    INSERT INTO [dbo].[WebAuthnCredential]
    (
        [Id],
        [UserId],
        [Name],
        [PublicKey],
        [DescriptorId],
        [Counter],
        [Type],
        [AaGuid],
        [UserKey],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @UserId,
        @Name,
        @PublicKey,
        @DescriptorId,
        @Counter,
        @Type,
        @AaGuid,
        @UserKey,
        @CreationDate,
        @RevisionDate
    )
END
