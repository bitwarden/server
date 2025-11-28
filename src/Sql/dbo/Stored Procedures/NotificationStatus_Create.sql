CREATE PROCEDURE [dbo].[NotificationStatus_Create]
    @NotificationId UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER,
    @ReadDate DATETIME2(7),
    @DeletedDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[NotificationStatus] (
        [NotificationId],
        [UserId],
        [ReadDate],
        [DeletedDate]
        )
    VALUES (
        @NotificationId,
        @UserId,
        @ReadDate,
        @DeletedDate
        )
END
