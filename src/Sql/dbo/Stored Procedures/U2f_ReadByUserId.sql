CREATE PROCEDURE [dbo].[U2f_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[U2fView]
    WHERE
        [UserId] = @UserId
END