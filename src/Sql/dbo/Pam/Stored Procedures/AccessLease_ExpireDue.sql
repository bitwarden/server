CREATE PROCEDURE [dbo].[AccessLease_ExpireDue]
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- Expiry is derived, not stored; PamLeaseExpirySweep's INSERT decides which run owns a lease.
    -- UPDLOCK/HOLDLOCK serializes concurrent sweeps; a loser re-checks after commit and skips it.
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

    -- No join needed; the caller audits/triggers off the lease row itself.
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
