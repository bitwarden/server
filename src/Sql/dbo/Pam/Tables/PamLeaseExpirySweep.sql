-- Journal of leases AccessLease_ExpireDue has already returned; expiry is derived, not stored.
-- Ensures the LeaseExpired audit event and access-end trigger fire a single time per lease.
CREATE TABLE [dbo].[PamLeaseExpirySweep] (
    [AccessLeaseId] UNIQUEIDENTIFIER    NOT NULL,
    [SweptDate]     DATETIME2 (7)       NOT NULL,
    CONSTRAINT [PK_PamLeaseExpirySweep] PRIMARY KEY CLUSTERED ([AccessLeaseId] ASC),
    CONSTRAINT [FK_PamLeaseExpirySweep_AccessLease] FOREIGN KEY ([AccessLeaseId]) REFERENCES [dbo].[AccessLease] ([Id]) ON DELETE CASCADE
);
