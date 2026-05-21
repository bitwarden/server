CREATE PROCEDURE [dbo].[OrganizationEventCleanup_ClaimNextPending]
    @Now DATETIME2(7),
    @LeaseExpiry DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    ;WITH [Pending] AS (
        SELECT TOP 1
            [Id],
            [OrganizationId],
            [CreationDate],
            [RevisionDate],
            [StartDate],
            [CompletedDate],
            [EventsDeletedCount],
            [Attempts],
            [LastError]
        FROM
            [dbo].[OrganizationEventCleanup] WITH (UPDLOCK, READPAST)
        WHERE
            [CompletedDate] IS NULL
            AND ([StartDate] IS NULL OR [RevisionDate] < @LeaseExpiry)
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
