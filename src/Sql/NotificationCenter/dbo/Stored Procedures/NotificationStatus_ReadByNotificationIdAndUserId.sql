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
