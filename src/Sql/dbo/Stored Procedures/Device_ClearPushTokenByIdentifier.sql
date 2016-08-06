CREATE PROCEDURE [dbo].[Device_ClearPushTokenByIdentifier]
    @Identifier NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[Device]
    SET
        [Identifier] = NULL
    WHERE
        [Identifier] = @Identifier
END
