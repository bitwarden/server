CREATE PROCEDURE [dbo].[OrganizationUserOrganizationDetails_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationUserOrganizationDetailsView]
    WHERE
        [UserId] = @UserId
END