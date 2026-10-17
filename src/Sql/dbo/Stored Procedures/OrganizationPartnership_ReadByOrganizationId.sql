CREATE PROCEDURE [dbo].[OrganizationPartnership_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationPartnershipView]
    WHERE
        [OrganizationId] = @OrganizationId
END
