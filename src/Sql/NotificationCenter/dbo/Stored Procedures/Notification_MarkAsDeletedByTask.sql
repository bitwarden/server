CREATE PROCEDURE [dbo].[Notification_MarkAsDeletedByTask]
    @TaskId UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    -- Collect NotificationIds as they are altered
    DECLARE @UserIdsForAlteredNotifications TABLE (
        UserId UNIQUEIDENTIFIER
    );

    -- Update existing NotificationStatus as deleted
    UPDATE ns
    SET ns.DeletedDate = GETUTCDATE()
    OUTPUT inserted.UserId INTO @UserIdsForAlteredNotifications
    FROM NotificationStatus ns
    INNER JOIN Notification n ON ns.NotificationId = n.Id
    WHERE n.TaskId = @TaskId
      AND ns.UserId = @UserId
      AND ns.DeletedDate IS NULL;

    -- Insert NotificationStatus records for notifications that don't have one yet
    INSERT INTO NotificationStatus (NotificationId, UserId, DeletedDate)
    OUTPUT inserted.UserId INTO @UserIdsForAlteredNotifications
    SELECT n.Id, @UserId, GETUTCDATE()
    FROM Notification n
    LEFT JOIN NotificationStatus ns
        ON n.Id = ns.NotificationId AND ns.UserId = @UserId
    WHERE n.TaskId = @TaskId
      AND ns.NotificationId IS NULL;

    -- Return the user ids associated with the altered notifications
    SELECT u.UserId
    FROM @UserIdsForAlteredNotifications u;
END
GO
