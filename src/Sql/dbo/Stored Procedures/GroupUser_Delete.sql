CREATE PROCEDURE [dbo].[GroupUser_Delete]
    @GroupId UNIQUEIDENTIFIER,
    @OrganizationUserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[GroupUser]
    WHERE
        [GroupId] = @GroupId
        AND [OrganizationUserId] = @OrganizationUserId

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationUserId] @OrganizationUserId
END