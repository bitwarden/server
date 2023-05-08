CREATE TABLE [dbo].[WebAuthnCredential] (
    [Id]                UNIQUEIDENTIFIER NOT NULL,
    [UserId]            UNIQUEIDENTIFIER NOT NULL,
    [Name]              NVARCHAR (50)    NOT NULL,
    [PublicKey]         VARCHAR (256)    NOT NULL,
    [DescriptorId]      VARCHAR (256)    NOT NULL,
    [Counter]           INT              NOT NULL,
    [Type]              VARCHAR (20)     NULL,
    [AaGuid]            UNIQUEIDENTIFIER NOT NULL,
    [UserKey]           VARCHAR (MAX)    NULL,
    [CreationDate]      DATETIME2 (7)    NOT NULL,
    [RevisionDate]      DATETIME2 (7)    NOT NULL,
    CONSTRAINT [PK_WebAuthnCredential] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_WebAuthnCredential_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id])
);

GO
CREATE NONCLUSTERED INDEX [IX_WebAuthnCredential_UserId]
    ON [dbo].[WebAuthnCredential]([UserId] ASC);

GO
CREATE VIEW [dbo].[WebAuthnCredentialView]
AS
SELECT
    *
FROM
    [dbo].[WebAuthnCredential]

GO
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

GO
CREATE PROCEDURE [dbo].[WebAuthnCredential_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[WebAuthnCredential]
    WHERE
        [Id] = @Id
END

GO
CREATE PROCEDURE [dbo].[WebAuthnCredential_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[WebAuthnCredentialView]
    WHERE
        [Id] = @Id
END

GO
CREATE PROCEDURE [dbo].[WebAuthnCredential_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[WebAuthnCredentialView]
    WHERE
        [UserId] = @UserId
END

GO
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