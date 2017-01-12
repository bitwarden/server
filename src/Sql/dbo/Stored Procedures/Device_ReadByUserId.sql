CREATE PROCEDURE [dbo].[Device_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[DeviceView]
    WHERE
        [UserId] = @UserId
END