-- Add CipherId column
IF COL_LENGTH('[dbo].[Send]', 'CipherId') IS NULL
BEGIN
    ALTER TABLE [dbo].[Send] 
        ADD [CipherId] UNIQUEIDENTIFIER NULL,
        CONSTRAINT [FK_Send_Cipher] FOREIGN KEY ([CipherId]) REFERENCES [dbo].[Cipher]([Id]);
END
GO

-- Refresh View
EXECUTE sp_refreshview N'[dbo].[SendView]'
GO

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
    @CipherId UNIQUEIDENTIFIER = NULL
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
        [CipherId]
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
            @CipherId
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
    @CipherId UNIQUEIDENTIFIER = NULL
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
        [CipherId] = @CipherId
    WHERE
        [Id] = @Id

    IF @UserId IS NOT NULL
    BEGIN
        EXEC [dbo].[User_BumpAccountRevisionDate] @UserId
    END
-- TODO: OrganizationId bump?
END
GO
