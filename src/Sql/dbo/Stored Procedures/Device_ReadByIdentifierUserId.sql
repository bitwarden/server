CREATE PROCEDURE [dbo].[Device_ReadByIdentifierUserId]
    @UserId UNIQUEIDENTIFIER,
    @Identifier NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[DeviceView]
    WHERE
        [UserId] = @UserId
        AND [Identifier] = @Identifier
END