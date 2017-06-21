CREATE PROCEDURE [dbo].[U2f_DeleteByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[U2f]
    WHERE
        [UserId] = @UserId
END