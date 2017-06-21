CREATE PROCEDURE [dbo].[Device_ClearPushTokenById]
    @Id NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[Device]
    SET
        [PushToken] = NULL
    WHERE
        [Id] = @Id
END