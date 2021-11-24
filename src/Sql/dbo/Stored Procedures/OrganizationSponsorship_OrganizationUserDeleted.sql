CREATE PROCEDURE [dbo].[OrganizationSponsorship_OrganizationUserDeleted]
    @OrganizationUserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        OS
    SET
        [SponsoringOrganizationUserId] = NULL
    FROM
        [dbo].[OrganizationSponsorship] OS
    WHERE
        [SponsoringOrganizationUserId] = @OrganizationUserId
END
GO
