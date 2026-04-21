CREATE PROCEDURE [dbo].[OrganizationEventCleanup_ReadNextPending]
AS
BEGIN
    SET NOCOUNT ON

    SELECT TOP 1
        *
    FROM
        [dbo].[OrganizationEventCleanup]
    WHERE
        [CompletedAt] IS NULL
    ORDER BY
        [QueuedAt] ASC
END
