CREATE PROCEDURE [dbo].[OrganizationSponsorship_OrganizationUserDeleted]
    @OrganizationUserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[OrganizationSponsorship]
    WHERE
        [SponsoringOrganizationUserId] = @OrganizationUserId
END
GO
