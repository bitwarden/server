CREATE PROCEDURE [dbo].[UserSigningKey_SetForRotation]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @KeyType TINYINT,
    @VerifyingKey VARCHAR(MAX),
    @SigningKey VARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    INSERT INTO [dbo].[UserSigningKey] ([Id], [UserId], [KeyType], [VerifyingKey], [SigningKey], [CreationDate], [RevisionDate])
    VALUES (@Id, @UserId, @KeyType, @VerifyingKey, @SigningKey, @CreationDate, @RevisionDate)
END
