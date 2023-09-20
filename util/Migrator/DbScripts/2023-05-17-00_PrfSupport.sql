IF COL_LENGTH('[dbo].[WebAuthnCredential]', 'PrfPublicKey') IS NULL
BEGIN
    ALTER TABLE
        [dbo].[WebAuthnCredential]
    ADD
        [PrfPublicKey] VARCHAR (MAX) NULL
END
GO

IF COL_LENGTH('[dbo].[WebAuthnCredential]', 'PrfPrivateKey') IS NULL
BEGIN
    ALTER TABLE
        [dbo].[WebAuthnCredential]
    ADD
        [PrfPrivateKey] VARCHAR (MAX) NULL
END
GO

IF COL_LENGTH('[dbo].[WebAuthnCredential]', 'SupportsPrf') IS NULL
BEGIN
    ALTER TABLE
        [dbo].[WebAuthnCredential]
    ADD
        [SupportsPrf] BIT NOT NULL DEFAULT 0
END
GO

IF OBJECT_ID('[dbo].[WebAuthnCredentialView]') IS NOT NULL
BEGIN
    DROP VIEW [dbo].[WebAuthnCredentialView]
END
GO

CREATE VIEW [dbo].[WebAuthnCredentialView]
AS
SELECT
    *
FROM
    [dbo].[WebAuthnCredential]

GO
CREATE OR ALTER PROCEDURE [dbo].[WebAuthnCredential_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @UserId UNIQUEIDENTIFIER,
    @Name NVARCHAR(50),
    @PublicKey VARCHAR (256),
    @DescriptorId VARCHAR(256),
    @Counter INT,
    @Type VARCHAR(20),
    @AaGuid UNIQUEIDENTIFIER,
    @UserKey VARCHAR (MAX),
    @PrfPublicKey VARCHAR (MAX),
    @PrfPrivateKey VARCHAR (MAX),
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
        [DescriptorId],
        [Counter],
        [Type],
        [AaGuid],
        [UserKey],
        [PrfPublicKey],
        [PrfPrivateKey],
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
        @DescriptorId,
        @Counter,
        @Type,
        @AaGuid,
        @UserKey,
        @PrfPublicKey,
        @PrfPrivateKey,
        @SupportsPrf,
        @CreationDate,
        @RevisionDate
    )
END

GO
CREATE OR ALTER PROCEDURE [dbo].[WebAuthnCredential_Update]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @Name NVARCHAR(50),
    @PublicKey VARCHAR (256),
    @DescriptorId VARCHAR(256),
    @Counter INT,
    @Type VARCHAR(20),
    @AaGuid UNIQUEIDENTIFIER,
    @UserKey VARCHAR (MAX),
    @PrfPublicKey VARCHAR (MAX),
    @PrfPrivateKey VARCHAR (MAX),
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
        [DescriptorId] = @DescriptorId,
        [Counter] = @Counter,
        [Type] = @Type,
        [AaGuid] = @AaGuid,
        [UserKey] = @UserKey,
        [PrfPublicKey] = @PrfPublicKey,
        [PrfPrivateKey] = @PrfPrivateKey,
        [SupportsPrf] = @SupportsPrf,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END
