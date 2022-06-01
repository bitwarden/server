CREATE PROCEDURE [dbo].[OrganizationSponsorship_OrganizationUsersDeleted]
    @SponsoringOrganizationUserIds [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        OS
    SET
        [ToDelete] = 1
    FROM
        [dbo].[OrganizationSponsorship] OS
    INNER JOIN
        @SponsoringOrganizationUserIds I ON I.Id = OS.SponsoringOrganizationUserID
END
GO
