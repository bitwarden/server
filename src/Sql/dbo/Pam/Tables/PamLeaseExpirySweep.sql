-- The natural-expiry sweep's journal: one row per lease AccessLease_ExpireDue has already returned. Expiry is
-- derived at read time rather than stored (a lease whose window closed on its own keeps Action = None forever), so
-- there is no status flip to mark a lease as processed -- this journal is what keeps the LeaseExpired audit event
-- and the rotation access-end trigger to at most one firing per lease.
CREATE TABLE [dbo].[PamLeaseExpirySweep] (
    [AccessLeaseId] UNIQUEIDENTIFIER    NOT NULL,
    [SweptDate]     DATETIME2 (7)       NOT NULL,
    CONSTRAINT [PK_PamLeaseExpirySweep] PRIMARY KEY CLUSTERED ([AccessLeaseId] ASC),
    CONSTRAINT [FK_PamLeaseExpirySweep_AccessLease] FOREIGN KEY ([AccessLeaseId]) REFERENCES [dbo].[AccessLease] ([Id]) ON DELETE CASCADE
);
