CREATE PROCEDURE [dbo].[SsoUser_ReadByUserIdOrganizationId]
    @UserId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[SsoUserView]
    WHERE
        [UserId] = @UserId
        AND [OrganizationId] = @OrganizationId
END
