CREATE PROCEDURE [dbo].[SubvaultUser_ReadByOrganizationUserId]
    @OrganizationUserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[SubvaultUserView]
    WHERE
        [OrganizationUserId] = @OrganizationUserId
END