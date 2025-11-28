-- Add optional TaskId column to Notification table
IF COL_LENGTH('[dbo].[Notification]', 'TaskId') IS NULL
BEGIN
    ALTER TABLE [dbo].[Notification]
        ADD [TaskId] UNIQUEIDENTIFIER NULL

    ALTER TABLE [dbo].[Notification]
        ADD CONSTRAINT [FK_Notification_SecurityTask] FOREIGN KEY ([TaskId]) REFERENCES [dbo].[SecurityTask] ([Id])
END
GO

IF NOT EXISTS (SELECT *
               FROM sys.indexes
               WHERE name = 'IX_Notification_TaskId')
    BEGIN
        CREATE NONCLUSTERED INDEX [IX_Notification_TaskId]
            ON [dbo].[Notification] ([TaskId] ASC) WHERE TaskId IS NOT NULL;
    END
GO

-- Alter Notification_Create and Notification_Update stored procedures to include TaskId
CREATE OR ALTER PROCEDURE [dbo].[Notification_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @Priority TINYINT,
    @Global BIT,
    @ClientType TINYINT,
    @UserId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Title NVARCHAR(256),
    @Body NVARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @TaskId UNIQUEIDENTIFIER = NULL
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
        [RevisionDate],
        [TaskId]
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
       @RevisionDate,
       @TaskId
   )
END
GO

CREATE OR ALTER PROCEDURE [dbo].[Notification_Update]
    @Id UNIQUEIDENTIFIER,
    @Priority TINYINT,
    @Global BIT,
    @ClientType TINYINT,
    @UserId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Title NVARCHAR(256),
    @Body NVARCHAR(MAX),
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7),
    @TaskId UNIQUEIDENTIFIER = NULL
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
        [RevisionDate] = @RevisionDate,
        [TaskId]       = @TaskId
    WHERE [Id] = @Id
END
GO

-- Recreate NotificationView
CREATE OR ALTER VIEW [dbo].[NotificationView]
AS
SELECT
    *
FROM
    [dbo].[Notification]
GO


