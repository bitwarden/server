CREATE OR ALTER PROCEDURE [dbo].[Notification_ReadActiveByTaskId]
    @TaskId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT n.*
    FROM [dbo].[NotificationView] n
    LEFT JOIN [dbo].[NotificationStatus] ns ON n.Id = ns.NotificationId
    WHERE n.[TaskId] = @TaskId
      AND ns.DeletedDate IS NULL
END
