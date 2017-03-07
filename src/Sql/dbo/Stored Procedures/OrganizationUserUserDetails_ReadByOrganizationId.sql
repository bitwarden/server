CREATE PROCEDURE [dbo].[OrganizationUserUserDetails_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationUserUserDetailsView]
    WHERE
        [OrganizationId] = @OrganizationId
END