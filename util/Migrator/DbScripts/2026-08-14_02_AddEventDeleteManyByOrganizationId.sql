-- Event: batched delete-by-organization used to purge orphaned event logs (GDPR).
CREATE OR ALTER PROCEDURE [dbo].[Event_DeleteManyByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER,
    @MaxRows INT = 50000
AS
BEGIN
    SET NOCOUNT ON

    -- Deletes at most @MaxRows rows per call so a single invocation stays well within the
    -- calling job's claim lease; the caller invokes repeatedly (refreshing its lease between
    -- calls) until 0 is returned.
    DECLARE @Total INT = 0
    DECLARE @BatchSize INT = 1000

    WHILE @BatchSize > 0 AND @Total < @MaxRows
    BEGIN
        DELETE TOP(@BatchSize)
        FROM
            [dbo].[Event]
        WHERE
            [OrganizationId] = @OrganizationId

        SET @BatchSize = @@ROWCOUNT
        SET @Total = @Total + @BatchSize
    END

    SELECT @Total
END
GO
