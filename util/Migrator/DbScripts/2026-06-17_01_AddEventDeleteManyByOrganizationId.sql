-- Event: batched delete-by-organization used to purge orphaned event logs (GDPR).
CREATE OR ALTER PROCEDURE [dbo].[Event_DeleteManyByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @Total INT = 0
    DECLARE @BatchSize INT = 1000

    WHILE @BatchSize > 0
    BEGIN
        BEGIN TRANSACTION Event_DeleteManyByOrganizationId

        DELETE TOP(@BatchSize)
        FROM
            [dbo].[Event]
        WHERE
            [OrganizationId] = @OrganizationId

        SET @BatchSize = @@ROWCOUNT
        SET @Total = @Total + @BatchSize

        COMMIT TRANSACTION Event_DeleteManyByOrganizationId
    END

    SELECT @Total
END
GO
