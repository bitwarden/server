CREATE TABLE [dbo].[Send] (
    [Id]             UNIQUEIDENTIFIER NOT NULL,
    [UserId]         UNIQUEIDENTIFIER NULL,
    [OrganizationId] UNIQUEIDENTIFIER NULL,
    [Type]           TINYINT          NOT NULL,
    [Data]           VARCHAR(MAX)     NOT NULL,
    [Key]            VARCHAR (MAX)    NOT NULL,
    [Password]       NVARCHAR (300)   NULL,
    [MaxAccessCount] INT              NULL,
    [AccessCount]    INT              NOT NULL,
    [CreationDate]   DATETIME2 (7)    NOT NULL,
    [RevisionDate]   DATETIME2 (7)    NOT NULL,
    [ExpirationDate] DATETIME2 (7)    NULL,
    [DeletionDate]   DATETIME2 (7)    NOT NULL,
    [Disabled]       BIT              NOT NULL,
    CONSTRAINT [PK_Send] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_Send_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]),
    CONSTRAINT [FK_Send_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id])
);
GO

CREATE NONCLUSTERED INDEX [IX_Send_UserId_OrganizationId]
    ON [dbo].[Send]([UserId] ASC, [OrganizationId] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_Send_DeletionDate]
    ON [dbo].[Send]([DeletionDate] ASC);
GO

CREATE VIEW [dbo].[SendView]
AS
SELECT
    *
FROM
    [dbo].[Send]
GO

IF OBJECT_ID('[dbo].[Send_Create]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Send_Create]
END
GO

CREATE PROCEDURE [dbo].[Send_Create]
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
    @Disabled BIT
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
        [Disabled]
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
        @Disabled
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

IF OBJECT_ID('[dbo].[Send_DeleteById]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Send_DeleteById]
END
GO

CREATE PROCEDURE [dbo].[Send_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @UserId UNIQUEIDENTIFIER
    DECLARE @OrganizationId UNIQUEIDENTIFIER
    DECLARE @Type TINYINT

    SELECT TOP 1
        @UserId = [UserId],
        @OrganizationId = [OrganizationId],
        @Type = [Type]
    FROM
        [dbo].[Send]
    WHERE
        [Id] = @Id

    DELETE
    FROM
        [dbo].[Send]
    WHERE
        [Id] = @Id

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

IF OBJECT_ID('[dbo].[Send_ReadById]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Send_ReadById]
END
GO

CREATE PROCEDURE [dbo].[Send_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[SendView]
    WHERE
        [Id] = @Id
END
GO

IF OBJECT_ID('[dbo].[Send_ReadByUserId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Send_ReadByUserId]
END
GO

CREATE PROCEDURE [dbo].[Send_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[SendView]
    WHERE
        [OrganizationId] IS NULL
        AND [UserId] = @UserId
END
GO

IF OBJECT_ID('[dbo].[Send_Update]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Send_Update]
END
GO

CREATE PROCEDURE [dbo].[Send_Update]
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
    @Disabled BIT
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
        [Disabled] = @Disabled
    WHERE
        [Id] = @Id

    IF @UserId IS NOT NULL
    BEGIN
        EXEC [dbo].[User_BumpAccountRevisionDate] @UserId
    END
    -- TODO: OrganizationId bump?
END
GO

IF OBJECT_ID('[dbo].[Organization_UpdateStorage]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Organization_UpdateStorage]
END
GO

CREATE PROCEDURE [dbo].[Organization_UpdateStorage]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @AttachmentStorage BIGINT
    DECLARE @SendStorage BIGINT

    CREATE TABLE #OrgStorageUpdateTemp
    ( 
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [Attachments] VARCHAR(MAX) NULL
    )

    INSERT INTO #OrgStorageUpdateTemp
    SELECT
        [Id],
        [Attachments]
    FROM
        [dbo].[Cipher]
    WHERE
        [UserId] IS NULL
        AND [OrganizationId] = @Id

    ;WITH [CTE] AS (
        SELECT
            [Id],
            (
                SELECT
                    SUM(CAST(JSON_VALUE(value,'$.Size') AS BIGINT))
                FROM
                    OPENJSON([Attachments])
            ) [Size]
        FROM
            #OrgStorageUpdateTemp
    )
    SELECT
        @AttachmentStorage = SUM([Size])
    FROM
        [CTE]

    DROP TABLE #OrgStorageUpdateTemp

    ;WITH [CTE] AS (
        SELECT
            [Id],
            CAST(JSON_VALUE([Data],'$.Size') AS BIGINT) [Size]
        FROM
            [Send]
        WHERE
            [UserId] IS NULL
            AND [OrganizationId] = @Id
    )
    SELECT
        @SendStorage = SUM([CTE].[Size])
    FROM
        [CTE]

    UPDATE
        [dbo].[Organization]
    SET
        [Storage] = (ISNULL(@AttachmentStorage, 0) + ISNULL(@SendStorage, 0)),
        [RevisionDate] = GETUTCDATE()
    WHERE
        [Id] = @Id
END
GO

IF OBJECT_ID('[dbo].[User_UpdateStorage]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[User_UpdateStorage]
END
GO

CREATE PROCEDURE [dbo].[User_UpdateStorage]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @AttachmentStorage BIGINT
    DECLARE @SendStorage BIGINT

    CREATE TABLE #UserStorageUpdateTemp
    ( 
        [Id] UNIQUEIDENTIFIER NOT NULL,
        [Attachments] VARCHAR(MAX) NULL
    )

    INSERT INTO #UserStorageUpdateTemp
    SELECT
        [Id],
        [Attachments]
    FROM
        [dbo].[Cipher]
    WHERE
        [UserId] = @Id

    ;WITH [CTE] AS (
        SELECT
            [Id],
            (
                SELECT
                    SUM(CAST(JSON_VALUE(value,'$.Size') AS BIGINT))
                FROM
                    OPENJSON([Attachments])
            ) [Size]
        FROM
            #UserStorageUpdateTemp
    )
    SELECT
        @AttachmentStorage = SUM([CTE].[Size])
    FROM
        [CTE]

    DROP TABLE #UserStorageUpdateTemp

    ;WITH [CTE] AS (
        SELECT
            [Id],
            CAST(JSON_VALUE([Data],'$.Size') AS BIGINT) [Size]
        FROM
            [Send]
        WHERE
            [UserId] = @Id
    )
    SELECT
        @SendStorage = SUM([CTE].[Size])
    FROM
        [CTE]

    UPDATE
        [dbo].[User]
    SET
        [Storage] = (ISNULL(@AttachmentStorage, 0) + ISNULL(@SendStorage, 0)),
        [RevisionDate] = GETUTCDATE()
    WHERE
        [Id] = @Id
END
GO

IF OBJECT_ID('[dbo].[Send_ReadByDeletionDateBefore]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Send_ReadByDeletionDateBefore]
END
GO

CREATE PROCEDURE [dbo].[Send_ReadByDeletionDateBefore]
    @DeletionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[SendView]
    WHERE
        [DeletionDate] < @DeletionDate
END
GO
