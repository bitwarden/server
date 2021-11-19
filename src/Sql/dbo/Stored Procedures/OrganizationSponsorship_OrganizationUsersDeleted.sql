CREATE PROCEDURE [dbo].[OrganizationSponsorship_OrganizationUsersDeleted]
    @SponsoringOrganizationUserIds [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @BatchSize AS INT;
    SET @BatchSize = 100;

    WHILE @BatchSize > 0
        BEGIN
        BEGIN TRANSACTION OrganizationSponsorship_DeleteOUs

        DELETE TOP(@BatchSize) OS
        FROM
            [dbo].[OrganizationSponsorship] OS
        INNER JOIN
            @SponsoringOrganizationUserIds I ON I.Id = OS.SponsoringOrganizationUserId

        SET @BatchSize = @@ROWCOUNT

        COMMIT TRANSACTION OrganizationSponsorship_DeleteOUs
    END
END
GO
