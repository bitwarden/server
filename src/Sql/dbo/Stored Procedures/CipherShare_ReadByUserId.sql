CREATE PROCEDURE [dbo].[CipherShare_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[CipherShareView]
    WHERE
        [UserId] = @UserId
        OR [ShareUserId] = @UserId
END