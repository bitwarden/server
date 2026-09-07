CREATE PROCEDURE [dbo].[AccessLease_ReadManyEndedByCollectionIds]
    @CollectionIds [dbo].[GuidIdArray] READONLY,
    @Since DATETIME2(7),
    @Now DATETIME2(7) = NULL
AS
BEGIN
    SET NOCOUNT ON

    -- Lets older callers omit @Now during rolling deployment.
    SET @Now = COALESCE(@Now, GETUTCDATE())

    -- Leases ended on/after @Since; "ended" derives from [Action] recording an early end.
    SELECT
        L.[Id],
        L.[AccessRequestId],
        L.[OrganizationId],
        L.[CollectionId],
        L.[CipherId],
        L.[RequesterId],
        L.[Action],
        L.[NotBefore],
        L.[NotAfter],
        L.[RevokedDate],
        L.[RevokedBy],
        L.[CreationDate]
    FROM
        [dbo].[AccessLease] L
        INNER JOIN @CollectionIds CI ON CI.[Id] = L.[CollectionId]
    WHERE
        -- Ended early (Revoked, Cancelled): its end is RevokedDate, whatever its window says.
        (L.[Action] IN (2, 3) AND L.[RevokedDate] >= @Since)
        -- Window closed on its own (end = NotAfter); byte 1 (retired stored Expired) is never matched.
        OR (L.[Action] = 0 AND L.[NotAfter] <= @Now AND L.[NotAfter] >= @Since)
    ORDER BY
        CASE WHEN L.[Action] IN (2, 3) THEN L.[RevokedDate] ELSE L.[NotAfter] END DESC
END
