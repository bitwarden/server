CREATE PROCEDURE [dbo].[OrganizationEventCleanup_ClaimNextPending]
    @Now DATETIME2(7),
    @StaleLeaseThreshold DATETIME2(7),
    @MaxFailureCount INT
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
            [FailureCount],
            [LastError]
        FROM
            [dbo].[OrganizationEventCleanup] WITH (UPDLOCK, READPAST)
        WHERE
            [CompletedDate] IS NULL
            AND ([StartDate] IS NULL OR [RevisionDate] < @StaleLeaseThreshold)
            AND [FailureCount] < @MaxFailureCount
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
        inserted.[FailureCount],
        inserted.[LastError]
END
