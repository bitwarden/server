CREATE PROCEDURE [dbo].[Device_ClearPushTokenByIdentifier]
    @Identifier NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[Device]
    SET
        [PushToken] = NULL
    WHERE
        [Identifier] = @Identifier
END