CREATE PROCEDURE [dbo].[CollectionUser_ReadByOrganizationUserId]
    @OrganizationUserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[CollectionUserView]
    WHERE
        [OrganizationUserId] = @OrganizationUserId
END