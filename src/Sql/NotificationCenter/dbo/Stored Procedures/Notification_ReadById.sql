CREATE PROCEDURE [dbo].[Notification_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT *
    FROM [dbo].[Notification]
    WHERE [Id] = @Id
END
