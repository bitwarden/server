CREATE PROCEDURE [dbo].[Notification_Update]
    @Id UNIQUEIDENTIFIER,
    @Priority TINYINT,
    @Global BIT,
    @ClientType TINYINT,
    @UserId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Title NVARCHAR(256),
    @Body NVARCHAR(MAX),
    @TaskId UNIQUEIDENTIFIER = NULL,
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
        [TaskId] = @TaskId,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate
    WHERE [Id] = @Id
END
