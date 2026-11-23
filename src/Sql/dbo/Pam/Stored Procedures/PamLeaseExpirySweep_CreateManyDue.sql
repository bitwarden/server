CREATE PROCEDURE [dbo].[PamLeaseExpirySweep_CreateManyDue]
    @Now DATETIME2(7),
    @Since DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- Expiry is derived, not stored; stamping [SweptDate] decides which run owns a lease. A concurrent sweep blocks
    -- on the row lock and re-checks [SweptDate] IS NULL afterwards, so it skips what the winner stamped.
    -- No join needed; the caller audits/triggers off the lease row itself.
    UPDATE
        [dbo].[AccessLease]
    SET
        [SweptDate] = @Now
    OUTPUT
        inserted.[Id],
        inserted.[OrganizationId],
        inserted.[CollectionId],
        inserted.[CipherId],
        inserted.[RequesterId],
        inserted.[NotBefore],
        inserted.[NotAfter]
    WHERE
        [Action] = 0 -- None: no early end recorded, so the closed window is a natural expiry
        AND [SweptDate] IS NULL
        AND [NotAfter] <= @Now
        AND [NotAfter] > @Since -- keeps the read from growing with lease history
END
