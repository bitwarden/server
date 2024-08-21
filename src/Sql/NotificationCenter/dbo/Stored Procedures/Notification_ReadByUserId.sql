CREATE PROCEDURE [dbo].[Notification_ReadByUserId]
    @UserId UNIQUEIDENTIFIER,
    @ClientType TINYINT
AS
BEGIN
    SET NOCOUNT ON

    SELECT *
    FROM [dbo].[Notification]
    WHERE (
            [ClientType] = @ClientType
            AND [UserId] = @UserId
            )
        OR [Global] = 1
    ORDER BY [CreationDate] DESC
END
