CREATE TABLE [dbo].[UserSigningKeys] (
    [Id]                        UNIQUEIDENTIFIER NOT NULL,
    [UserId]                    UNIQUEIDENTIFIER,
    [KeyType]                   TINYINT NOT NULL,
    [VerifyingKey]              VARCHAR(MAX) NOT NULL,
    [SigningKey]                VARCHAR(MAX) NOT NULL,
    [CreationDate]              DATETIME2 (7) NOT NULL,
    [RevisionDate]              DATETIME2 (7) NOT NULL,
    CONSTRAINT [PK_UserSigningKeys] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_UserSigningKeys_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id])
);
GO

CREATE PROCEDURE [dbo].[UserSigningKeys_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SELECT *
    FROM [dbo].[UserSigningKeys]
    WHERE [UserId] = @UserId;
END
GO

CREATE PROCEDURE [dbo].[UserSigningKeys_UpdateForRotation]
    @UserId UNIQUEIDENTIFIER,
    @KeyType TINYINT,
    @VerifyingKey VARCHAR(MAX),
    @SigningKey VARCHAR(MAX),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    UPDATE [dbo].[UserSigningKeys]
    SET [KeyType] = @KeyType,
        [VerifyingKey] = @VerifyingKey,
        [SigningKey] = @SigningKey,
        [RevisionDate] = @RevisionDate
    WHERE [UserId] = @UserId;
END
GO

CREATE PROCEDURE [dbo].[UserSigningKeys_SetForRotation]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @KeyType TINYINT,
    @VerifyingKey VARCHAR(MAX),
    @SigningKey VARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    INSERT INTO [dbo].[UserSigningKeys] ([Id], [UserId], [KeyType], [VerifyingKey], [SigningKey], [CreationDate], [RevisionDate])
    VALUES (@Id, @UserId, @KeyType, @VerifyingKey, @SigningKey, @CreationDate, @RevisionDate)
END
GO
