CREATE PROCEDURE [dbo].[Notification_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT *
    FROM [dbo].[NotificationView]
    WHERE [Id] = @Id
END
