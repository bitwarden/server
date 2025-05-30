CREATE OR ALTER PROCEDURE [dbo].[Notification_MarkAsDeletedByTask]
    @TaskId UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    -- Collect NotificationIds as they are altered
    DECLARE @AlteredNotifications TABLE (
        NotificationId UNIQUEIDENTIFIER
    );

    -- Update existing NotificationStatus as deleted
    UPDATE ns
    SET ns.DeletedDate = GETUTCDATE()
    OUTPUT inserted.NotificationId INTO @AlteredNotifications
    FROM NotificationStatus ns
    INNER JOIN Notification n ON ns.NotificationId = n.Id
    WHERE n.TaskId = @TaskId
      AND ns.UserId = @UserId
      AND ns.DeletedDate IS NULL;

    -- Insert NotificationStatus records for notifications that don't have one yet
    INSERT INTO NotificationStatus (NotificationId, UserId, DeletedDate)
    OUTPUT inserted.NotificationId INTO @AlteredNotifications
    SELECT n.Id, @UserId, GETUTCDATE()
    FROM Notification n
    LEFT JOIN NotificationStatus ns
        ON n.Id = ns.NotificationId AND ns.UserId = @UserId
    WHERE n.TaskId = @TaskId
      AND ns.NotificationId IS NULL;

    -- Return all notifications that have been altered
    SELECT n.*
    FROM Notification n
    INNER JOIN @AlteredNotifications a ON n.Id = a.NotificationId;
END
GO
