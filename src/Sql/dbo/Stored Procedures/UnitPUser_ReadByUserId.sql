CREATE PROCEDURE [dbo].[UnitPUser_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[UnitPUserView]
    WHERE
        [UserId] = @UserId
END
