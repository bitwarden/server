CREATE PROCEDURE [dbo].[NotificationStatus_Update]
    @NotificationId UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @ReadDate DATETIME2(7),
    @DeletedDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE [dbo].[NotificationStatus]
    SET [ReadDate] = @ReadDate,
        [DeletedDate] = @DeletedDate
    WHERE [NotificationId] = @NotificationId
        AND [UserId] = @UserId
END
