CREATE PROCEDURE [dbo].[OrganizationSponsorship_DeleteByIds]
    @Ids [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @BatchSize INT = 100

    WHILE @BatchSize > 0
        BEGIN
            BEGIN TRANSACTION OrgSponsorship_DeleteMany

            DELETE TOP(@BatchSize) OS
            FROM
                [dbo].[OrganizationSponsorship] OS
            INNER JOIN
                @Ids I ON I.Id = OS.Id

            SET @BatchSize = @@ROWCOUNT

            COMMIT TRANSACTION OrgSponsorship_DeleteMany
        END
END