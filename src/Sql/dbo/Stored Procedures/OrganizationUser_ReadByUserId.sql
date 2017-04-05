CREATE PROCEDURE [dbo].[OrganizationUser_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationUserView]
    WHERE
        [UserId] = @UserId
END