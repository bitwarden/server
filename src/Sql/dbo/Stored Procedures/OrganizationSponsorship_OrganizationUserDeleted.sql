CREATE PROCEDURE [dbo].[OrganizationSponsorship_OrganizationUserDeleted]
    @OrganizationUserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        OS
    SET
        [ToDelete] = 1
    FROM
        [dbo].[OrganizationSponsorship] OS
    WHERE
        [SponsoringOrganizationUserID] = @OrganizationUserId
END
GO
