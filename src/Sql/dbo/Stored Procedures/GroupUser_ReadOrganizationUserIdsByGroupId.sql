CREATE PROCEDURE [dbo].[GroupUser_ReadOrganizationUserIdsByGroupId]
    @GroupId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        [OrganizationUserId]
    FROM
        [dbo].[GroupUser]
    WHERE
        [GroupId] = @GroupId
END