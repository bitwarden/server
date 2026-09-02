-- Update Send table to add EmailHashes Column
IF COL_LENGTH('[dbo].[Send]', 'EmailHashes') IS NULL
BEGIN
ALTER TABLE [dbo].[Send]
    ADD [EmailHashes] NVARCHAR(4000) NULL;
END
GO

-- Update Send_Create to include EmailHashes column
CREATE OR ALTER PROCEDURE [dbo].[Send_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @UserId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Type TINYINT,
    @Data VARCHAR(MAX),
    @Key VARCHAR(MAX),
    @Password NVARCHAR(300),
    @MaxAccessCount INT,
    @AccessCount INT,
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @ExpirationDate DATETIME2(7),
    @DeletionDate DATETIME2(7),
    @Disabled BIT,
    @HideEmail BIT,
    @CipherId UNIQUEIDENTIFIER = NULL,
    @Emails NVARCHAR(4000) = NULL,
    @AuthType TINYINT = NULL,
    @EmailHashes NVARCHAR(4000) = NULL
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[Send]
    (
        [Id],
        [UserId],
        [OrganizationId],
        [Type],
        [Data],
        [Key],
        [Password],
        [MaxAccessCount],
        [AccessCount],
        [CreationDate],
        [RevisionDate],
        [ExpirationDate],
        [DeletionDate],
        [Disabled],
        [HideEmail],
        [CipherId],
        [Emails],
        [AuthType],
        [EmailHashes]
    )
    VALUES
    (
        @Id,
        @UserId,
        @OrganizationId,
        @Type,
        @Data,
        @Key,
        @Password,
        @MaxAccessCount,
        @AccessCount,
        @CreationDate,
        @RevisionDate,
        @ExpirationDate,
        @DeletionDate,
        @Disabled,
        @HideEmail,
        @CipherId,
        @Emails,
        @AuthType,
        @EmailHashes
    )

    IF @UserId IS NOT NULL
    BEGIN
        IF @Type = 1 --File
        BEGIN
            EXEC [dbo].[User_UpdateStorage] @UserId
        END
        EXEC [dbo].[User_BumpAccountRevisionDate] @UserId
    END
    -- TODO: OrganizationId bump?
END
GO

-- Update Send_Update to include EmailHashes column
CREATE OR ALTER PROCEDURE [dbo].[Send_Update]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Type TINYINT,
    @Data VARCHAR(MAX),
    @Key VARCHAR(MAX),
    @Password NVARCHAR(300),
    @MaxAccessCount INT,
    @AccessCount INT,
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @ExpirationDate DATETIME2(7),
    @DeletionDate DATETIME2(7),
    @Disabled BIT,
    @HideEmail BIT,
    @CipherId UNIQUEIDENTIFIER = NULL,
    @Emails NVARCHAR(4000) = NULL,
    @AuthType TINYINT = NULL,
    @EmailHashes NVARCHAR(4000) = NULL
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[Send]
    SET
        [UserId] = @UserId,
        [OrganizationId] = @OrganizationId,
        [Type] = @Type,
        [Data] = @Data,
        [Key] = @Key,
        [Password] = @Password,
        [MaxAccessCount] = @MaxAccessCount,
        [AccessCount] = @AccessCount,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate,
        [ExpirationDate] = @ExpirationDate,
        [DeletionDate] = @DeletionDate,
        [Disabled] = @Disabled,
        [HideEmail] = @HideEmail,
        [CipherId] = @CipherId,
        [Emails] = @Emails,
        [AuthType] = @AuthType,
        [EmailHashes] = @EmailHashes
    WHERE
        [Id] = @Id

    IF @UserId IS NOT NULL
    BEGIN
        EXEC [dbo].[User_BumpAccountRevisionDate] @UserId
    END
    -- TODO: OrganizationId bump?
END
GO
EXECUTE sp_refreshview N'[dbo].[SendView]'
GO
