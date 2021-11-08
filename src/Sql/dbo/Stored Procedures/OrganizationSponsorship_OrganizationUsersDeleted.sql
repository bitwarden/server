CREATE PROCEDURE [dbo].[OrganizationSponsorship_OrganizationUsersDeleted]
    @SponsoringOrganizationUserIds [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    SET @BatchSize = 100;

    WHILE @BatchSize > 0
        BEGIN
        BEGIN TRANSACTION OrganizationSponsorship_DeleteOUs

        DELETE TOP(@BatchSize) OS
        FROM
            [dbo].[OrganiozationSponsorship] OS
        INNER JOIN
            @Ids I ON I.Id = OS.SponsoringOrganizationUserId

        SET @BatchSize = @@ROWCOUNT

        COMMIT TRANSACTION OrganizationSponsorship_DeleteOUs
    END
END
GO
