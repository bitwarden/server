CREATE PROCEDURE [dbo].[NotificationId_ReadByNotificationIdAndUserId]
    @NotificationId UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT *
    FROM [dbo].[NotificationStatus]
    WHERE [NotificationId] = @NotificationId
        AND [UserId] = @UserId
END
