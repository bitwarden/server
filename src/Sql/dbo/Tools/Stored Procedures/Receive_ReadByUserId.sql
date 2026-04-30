CREATE PROCEDURE [dbo].[Receive_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[ReceiveView]
    WHERE
        [UserId] = @UserId
END
