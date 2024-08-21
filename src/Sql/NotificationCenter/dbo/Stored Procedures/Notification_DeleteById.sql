CREATE PROCEDURE [dbo].[Notification_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM [dbo].[Notification]
    WHERE [Id] = @Id
END
