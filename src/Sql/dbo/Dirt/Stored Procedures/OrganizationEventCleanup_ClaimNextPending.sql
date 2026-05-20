CREATE PROCEDURE [dbo].[OrganizationEventCleanup_ClaimNextPending]
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    ;WITH [Pending] AS (
        SELECT TOP 1
            *
        FROM
            [dbo].[OrganizationEventCleanup] WITH (UPDLOCK, READPAST)
        WHERE
            [CompletedDate] IS NULL
        ORDER BY
            [CreationDate] ASC
    )
    UPDATE [Pending]
    SET
        [StartDate] = COALESCE([StartDate], @Now),
        [RevisionDate] = @Now
    OUTPUT
        inserted.[Id],
        inserted.[OrganizationId],
        inserted.[CreationDate],
        inserted.[RevisionDate],
        inserted.[StartDate],
        inserted.[CompletedDate],
        inserted.[EventsDeletedCount],
        inserted.[Attempts],
        inserted.[LastError]
END
