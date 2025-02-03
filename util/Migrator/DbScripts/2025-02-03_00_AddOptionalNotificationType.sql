-- Add optional Type column to Notification table
IF COL_LENGTH('[dbo].[Notification]', 'Type') IS NULL
BEGIN
    ALTER TABLE [dbo].[Notification]
    ADD [Type] TINYINT NULL
END
GO

-- Alter Notification_Create and Notification_Update stored procedures to include Type
CREATE OR ALTER PROCEDURE [dbo].[Notification_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @Priority TINYINT,
    @Global BIT,
    @ClientType TINYINT,
    @UserId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Title NVARCHAR(256),
    @Body NVARCHAR(MAX),
    @Type TINYINT = NULL,
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
        [Type],
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
       @Type,
       @CreationDate,
       @RevisionDate
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
    @Type TINYINT = NULL,
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
        [Type] = @Type,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate
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


