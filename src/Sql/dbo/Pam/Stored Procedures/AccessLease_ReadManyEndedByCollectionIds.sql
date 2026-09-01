CREATE PROCEDURE [dbo].[AccessLease_ReadManyEndedByCollectionIds]
    @CollectionIds [dbo].[GuidIdArray] READONLY,
    @Since DATETIME2(7),
    @Now DATETIME2(7) = NULL
AS
BEGIN
    SET NOCOUNT ON

    -- @Now defaults so a rolling deployment stays safe: an older server that predates this parameter calls the
    -- procedure without it and gets the database clock, which filters the same way.
    SET @Now = COALESCE(@Now, GETUTCDATE())

    -- Governance history: leases that have ended (derived Expired, or ended early) on the supplied
    -- (caller-manageable) collections, that ended on or after @Since. An ended-early lease's end is its RevokedDate;
    -- an expired lease's end is its NotAfter. Most recently ended first.
    --
    -- "Ended" has to be derived, not read: [Action] only ever records an early end, so a lease whose window simply
    -- closed carries 0 (None) forever, and only the clock can call it Expired. Only stored facts leave this read;
    -- the derived status is computed at the repository boundary from the same columns.
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
        -- Window closed on its own: its end is NotAfter. Byte 1 (the retired stored Expired) is deliberately NOT
        -- matched: nothing ever wrote it, and ComputeLeaseStatus has no arm for it, so reading such a stray row
        -- would fail the whole endpoint. Not read means not derived -- it simply stays invisible.
        OR (L.[Action] = 0 AND L.[NotAfter] <= @Now AND L.[NotAfter] >= @Since)
    ORDER BY
        CASE WHEN L.[Action] IN (2, 3) THEN L.[RevokedDate] ELSE L.[NotAfter] END DESC
END
