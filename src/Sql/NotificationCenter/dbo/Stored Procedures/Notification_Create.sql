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
