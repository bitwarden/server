IF EXISTS (
    SELECT *
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE COLUMN_NAME = 'Emails' AND
        DATA_TYPE = 'nvarchar' AND
        TABLE_NAME = 'Send' AND
        CHARACTER_MAXIMUM_LENGTH = 1024)
BEGIN
    ALTER TABLE [dbo].[Send]
    ALTER COLUMN [Emails] NVARCHAR (4000) NULL
END
GO

-- Update Send_Create to use NVARCHAR(4000) for Emails
CREATE OR ALTER PROCEDURE [dbo].[Send_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @UserId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Type TINYINT,
    @AuthType TINYINT,
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
    @Emails NVARCHAR(4000) = NULL
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[Send]
    (
        [Id],
        [UserId],
        [OrganizationId],
        [Type],
        [AuthType],
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
        [Emails]
    )
    VALUES
    (
        @Id,
        @UserId,
        @OrganizationId,
        @Type,
        @AuthType,
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
        @Emails
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

-- Update Send_Update to use NVARCHAR(4000) for Emails
CREATE OR ALTER PROCEDURE [dbo].[Send_Update]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Type TINYINT,
    @AuthType TINYINT,
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
    @Emails NVARCHAR(4000) = NULL
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[Send]
    SET
        [UserId] = @UserId,
        [OrganizationId] = @OrganizationId,
        [Type] = @Type,
        [AuthType] = @AuthType,
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
        [Emails] = @Emails
    WHERE
        [Id] = @Id

    IF @UserId IS NOT NULL
    BEGIN
        EXEC [dbo].[User_BumpAccountRevisionDate] @UserId
    END
    -- TODO: OrganizationId bump?
END
GO
