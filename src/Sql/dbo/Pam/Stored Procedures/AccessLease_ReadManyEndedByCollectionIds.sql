CREATE PROCEDURE [dbo].[AccessLease_ReadManyEndedByCollectionIds]
    @CollectionIds [dbo].[GuidIdArray] READONLY,
    @Since DATETIME2(7),
    @Now DATETIME2(7) = NULL
AS
BEGIN
    SET NOCOUNT ON

    -- @Now defaults so a rolling deployment stays safe: an older server that predates this parameter calls the
    -- procedure without it and gets the database clock, which projects the same result.
    SET @Now = COALESCE(@Now, GETUTCDATE())

    -- Governance history: leases that have ended (Expired, Revoked, or Cancelled) on the supplied (caller-manageable)
    -- collections, that ended on or after @Since. A revoked/cancelled lease's end is its RevokedDate; an expired
    -- lease's end is its NotAfter. Most recently ended first.
    --
    -- "Ended" has to be derived, not read: [Status] only ever records an early end, so a lease whose window simply
    -- closed keeps Status 0 (Active) forever. Filtering on [Status] IN (1, 2, 3) therefore matched no naturally
    -- expired lease at all, and every one of them fell out of this view entirely -- out of the active read (its
    -- window has closed) and never into this one (PM-42355). Both the filter and the projected [Status] below
    -- reinterpret a closed-window Active row as Expired, mirroring AccessLease.StatusAsOf.
    SELECT
        L.[Id],
        L.[AccessRequestId],
        L.[OrganizationId],
        L.[CollectionId],
        L.[CipherId],
        L.[RequesterId],
        CASE WHEN L.[Status] = 0 AND L.[NotAfter] <= @Now THEN 1 ELSE L.[Status] END AS [Status],
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
        (L.[Status] IN (2, 3) AND L.[RevokedDate] >= @Since)
        -- Window closed: its end is NotAfter. Status 0 is the case that was missing; Status 1 stays matched so a
        -- row that does carry a stored Expired is still read.
        OR (L.[Status] IN (0, 1) AND L.[NotAfter] <= @Now AND L.[NotAfter] >= @Since)
    ORDER BY
        CASE WHEN L.[Status] IN (2, 3) THEN L.[RevokedDate] ELSE L.[NotAfter] END DESC
END
