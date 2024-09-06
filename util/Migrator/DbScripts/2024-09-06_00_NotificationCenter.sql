-- Notification

-- Table Notification
IF OBJECT_ID('[dbo].[Notification]') IS NULL
    BEGIN
        CREATE TABLE [dbo].[Notification]
        (
            [Id] UNIQUEIDENTIFIER NOT NULL,
            [Priority] TINYINT NOT NULL,
            [Global] BIT NOT NULL,
            [ClientType] TINYINT NOT NULL,
            [UserId] UNIQUEIDENTIFIER NULL,
            [OrganizationId] UNIQUEIDENTIFIER NULL,
            [Title] NVARCHAR (256) NULL,
            [Body] NVARCHAR (MAX) NULL,
            [CreationDate] DATETIME2 (7) NOT NULL,
            [RevisionDate] DATETIME2 (7) NOT NULL,
            CONSTRAINT [PK_Notification] PRIMARY KEY CLUSTERED ([Id] ASC),
            CONSTRAINT [FK_Notification_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]),
            CONSTRAINT [FK_Notification_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id])
        );

        CREATE NONCLUSTERED INDEX [IX_Notification_Priority_CreationDate_ClientType_Global_UserId_OrganizationId]
            ON [dbo].[Notification]([Priority] DESC, [CreationDate] DESC, [ClientType], [Global], [UserId], [OrganizationId]);

        CREATE NONCLUSTERED INDEX [IX_Notification_UserId]
            ON [dbo].[Notification]([UserId] ASC) WHERE UserId IS NOT NULL;

        CREATE NONCLUSTERED INDEX [IX_Notification_OrganizationId]
            ON [dbo].[Notification]([OrganizationId] ASC) WHERE OrganizationId IS NOT NULL;
END
GO

-- Table NotificationStatus
IF OBJECT_ID('[dbo].[NotificationStatus]') IS NULL
    BEGIN
        CREATE TABLE [dbo].[NotificationStatus]
        (
            [NotificationId] UNIQUEIDENTIFIER NOT NULL,
            [UserId] UNIQUEIDENTIFIER NOT NULL,
            [ReadDate] DATETIME2 (7) NULL,
            [DeletedDate] DATETIME2 (7) NULL,
            CONSTRAINT [PK_NotificationStatus] PRIMARY KEY CLUSTERED ([NotificationId] ASC, [UserId] ASC),
            CONSTRAINT [FK_NotificationStatus_User] FOREIGN KEY ([UserId]) REFERENCES [dbo].[User] ([Id])
        );
    END
GO

-- Stored Procedures: Create
IF OBJECT_ID('[dbo].[Notification_Create]') IS NOT NULL
    BEGIN
        DROP PROCEDURE [dbo].[Notification_Create]
    END
GO

CREATE PROCEDURE [dbo].[Notification_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @Priority TINYINT,
    @Global BIT,
    @ClientType TINYINT,
    @UserId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Title NVARCHAR(256),
    @Body NVARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[Notification] (
        [Id],
        [Priority],
        [Global],
        [ClientType],
        [UserId],
        [OrganizationId],
        [Title],
        [Body],
        [CreationDate],
        [RevisionDate]
    )
    VALUES (
               @Id,
               @Priority,
               @Global,
               @ClientType,
               @UserId,
               @OrganizationId,
               @Title,
               @Body,
               @CreationDate,
               @RevisionDate
           )
END
GO

-- Stored Procedure: ReadById
IF OBJECT_ID('[dbo].[Notification_ReadById]') IS NOT NULL
    BEGIN
        DROP PROCEDURE [dbo].[Notification_ReadById]
    END
GO

CREATE PROCEDURE [dbo].[Notification_ReadById]
@Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT *
    FROM [dbo].[Notification]
    WHERE [Id] = @Id
END
GO

-- Stored Procedure: ReadByUserId
IF OBJECT_ID('[dbo].[Notification_ReadByUserId]') IS NOT NULL
    BEGIN
        DROP PROCEDURE [dbo].[Notification_ReadByUserId]
    END
GO

CREATE PROCEDURE [dbo].[Notification_ReadByUserId]
    @UserId UNIQUEIDENTIFIER,
    @ClientType TINYINT
AS
BEGIN
    SET NOCOUNT ON

    SELECT [Notification].*
    FROM [dbo].[Notification]
             LEFT JOIN
         [dbo].[OrganizationUser] ON [Notification].[OrganizationId] = [OrganizationUser].[OrganizationId]
             AND [OrganizationUser].[UserId] = @UserId
    WHERE [ClientType] IN (0, CASE WHEN @ClientType != 0 THEN @ClientType END)
      AND ([Global] = 1
        OR ([Notification].[UserId] = @UserId
            AND ([Notification].[OrganizationId] IS NULL
                OR [OrganizationUser].[OrganizationId] IS NOT NULL))
        OR ([Notification].[UserId] IS NULL
            AND [OrganizationUser].[OrganizationId] IS NOT NULL))
    ORDER BY [Priority] DESC, [Notification].[CreationDate] DESC
END
GO

-- Stored Procedure: ReadByUserIdAndStatus
IF OBJECT_ID('[dbo].[Notification_ReadByUserIdAndStatus]') IS NOT NULL
    BEGIN
        DROP PROCEDURE [dbo].[Notification_ReadByUserIdAndStatus]
    END
GO

CREATE PROCEDURE [dbo].[Notification_ReadByUserIdAndStatus]
    @UserId UNIQUEIDENTIFIER,
    @ClientType TINYINT,
    @Read BIT,
    @Deleted BIT
AS
BEGIN
    SET NOCOUNT ON

    SELECT [Notification].*
    FROM [dbo].[Notification]
             LEFT JOIN [dbo].[OrganizationUser] ON [Notification].[OrganizationId] = [OrganizationUser].[OrganizationId]
        AND [OrganizationUser].[UserId] = @UserId
             JOIN [dbo].[NotificationStatus] ON [Notification].[Id] = [NotificationStatus].[NotificationId]
        AND [NotificationStatus].[UserId] = @UserId
    WHERE [ClientType] IN (0, CASE WHEN @ClientType != 0 THEN @ClientType END)
      AND ([Global] = 1
        OR ([Notification].[UserId] = @UserId
            AND ([Notification].[OrganizationId] IS NULL
                OR [OrganizationUser].[OrganizationId] IS NOT NULL))
        OR ([Notification].[UserId] IS NULL
            AND [OrganizationUser].[OrganizationId] IS NOT NULL))
      AND (@Read IS NULL
        OR IIF((@Read = 1 AND [NotificationStatus].[ReadDate] IS NOT NULL) OR
               (@Read = 0 AND [NotificationStatus].[ReadDate] IS NULL),
               1, 0) = 1
        OR @Deleted IS NULL
        OR IIF((@Deleted = 1 AND [NotificationStatus].[DeletedDate] IS NOT NULL) OR
               (@Deleted = 0 AND [NotificationStatus].[DeletedDate] IS NULL),
               1, 0) = 1)
    ORDER BY [Priority] DESC, [Notification].[CreationDate] DESC
END
GO

-- Stored Procedure: Update
IF OBJECT_ID('[dbo].[Notification_Update]') IS NOT NULL
    BEGIN
        DROP PROCEDURE [dbo].[Notification_Update]
    END
GO

CREATE PROCEDURE [dbo].[Notification_Update]
    @Id UNIQUEIDENTIFIER,
    @Priority TINYINT,
    @Global BIT,
    @ClientType TINYINT,
    @UserId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Title NVARCHAR(256),
    @Body NVARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE [dbo].[Notification]
    SET [Priority] = @Priority,
        [Global] = @Global,
        [ClientType] = @ClientType,
        [UserId] = @UserId,
        [OrganizationId] = @OrganizationId,
        [Title] = @Title,
        [Body] = @Body,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate
    WHERE [Id] = @Id
END
GO


-- NotificationStatus

-- Stored Procedure: Create
IF OBJECT_ID('[dbo].[NotificationStatus_Create]') IS NOT NULL
    BEGIN
        DROP PROCEDURE [dbo].[NotificationStatus_Create]
    END
GO

CREATE PROCEDURE [dbo].[NotificationStatus_Create]
    @NotificationId UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @ReadDate DATETIME2(7),
    @DeletedDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[NotificationStatus] (
        [NotificationId],
        [UserId],
        [ReadDate],
        [DeletedDate]
    )
    VALUES (
               @NotificationId,
               @UserId,
               @ReadDate,
               @DeletedDate
           )
END
GO

-- Stored Procedure: ReadByNotificationIdAndUserId
IF OBJECT_ID('[dbo].[NotificationStatus_ReadByNotificationIdAndUserId]') IS NOT NULL
    BEGIN
        DROP PROCEDURE [dbo].[NotificationStatus_ReadByNotificationIdAndUserId]
    END
GO

CREATE PROCEDURE [dbo].[NotificationStatus_ReadByNotificationIdAndUserId]
    @NotificationId UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT TOP 1 *
    FROM [dbo].[NotificationStatus]
    WHERE [NotificationId] = @NotificationId
      AND [UserId] = @UserId
END
GO

-- Stored Procedure: Update
IF OBJECT_ID('[dbo].[NotificationStatus_Update]') IS NOT NULL
    BEGIN
        DROP PROCEDURE [dbo].[NotificationStatus_Update]
    END
GO

CREATE PROCEDURE [dbo].[NotificationStatus_Update]
    @NotificationId UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @ReadDate DATETIME2(7),
    @DeletedDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE [dbo].[NotificationStatus]
    SET [ReadDate] = @ReadDate,
        [DeletedDate] = @DeletedDate
    WHERE [NotificationId] = @NotificationId
      AND [UserId] = @UserId
END
GO
