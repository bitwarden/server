CREATE PROCEDURE [dbo].[OrganizationUserDetails_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationUserDetailsView]
    WHERE
        [OrganizationId] = @OrganizationId
END