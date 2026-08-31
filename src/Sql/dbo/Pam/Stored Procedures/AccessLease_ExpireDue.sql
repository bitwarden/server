CREATE PROCEDURE [dbo].[AccessLease_ExpireDue]
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- The lease natural-expiry sweep. Expiry is derived rather than stored (a lease whose window closed on its own
    -- keeps [Action] = 0 forever), so there is no status flip to mark a lease as processed. The
    -- [PamLeaseExpirySweep] journal is the once-only arbiter instead: the INSERT decides which run owns a lease, so
    -- the LeaseExpired audit event and the rotation access-end trigger fire at most once per lease. UPDLOCK/HOLDLOCK
    -- on the journal probe serializes concurrent sweeps over the same rows -- a losing run re-evaluates the probe
    -- after the winner commits and skips the lease; the primary key backstops the pattern.
    DECLARE @Due TABLE ([AccessLeaseId] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY);

    INSERT INTO [dbo].[PamLeaseExpirySweep] ([AccessLeaseId], [SweptDate])
    OUTPUT inserted.[AccessLeaseId] INTO @Due
    SELECT
        AL.[Id],
        @Now
    FROM [dbo].[AccessLease] AL
    WHERE AL.[Action] = 0 -- None: no early end recorded, so the closed window is a natural expiry
        AND AL.[NotAfter] <= @Now
        AND NOT EXISTS (
            SELECT 1
            FROM [dbo].[PamLeaseExpirySweep] S WITH (UPDLOCK, HOLDLOCK)
            WHERE S.[AccessLeaseId] = AL.[Id]
        )

    -- No join was needed for the projection under the old flip design, and none is for the columns here either --
    -- everything the caller audits/triggers on lives on the lease row itself.
    SELECT
        AL.[Id],
        AL.[OrganizationId],
        AL.[CollectionId],
        AL.[CipherId],
        AL.[RequesterId],
        AL.[NotBefore],
        AL.[NotAfter]
    FROM [dbo].[AccessLease] AL
    INNER JOIN @Due D ON D.[AccessLeaseId] = AL.[Id]
END
