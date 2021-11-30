CREATE PROCEDURE [dbo].[OrganizationSponsorship_OrganizationUsersDeleted]
    @SponsoringOrganizationUserIds [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        OS
    SET
        [SponsoringOrganizationUserId] = NULL
    FROM
        [dbo].[OrganizationSponsorship] OS
    INNER JOIN
        @SponsoringOrganizationUserIds I ON I.Id = OS.SponsoringOrganizationUserId
END
GO
