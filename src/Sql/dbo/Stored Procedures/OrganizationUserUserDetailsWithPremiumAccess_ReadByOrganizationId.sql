CREATE PROCEDURE [dbo].[OrganizationUserUserDetailsWithPremiumAccess_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationUserUserDetailsWithPremiumAccessView]
    WHERE
        [OrganizationId] = @OrganizationId
END
