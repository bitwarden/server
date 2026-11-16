-- Replaces the PamLeaseExpirySweep journal with [AccessLease].[SweptDate], so the expiry sweep is one atomic UPDATE
-- over unswept rows instead of an anti-join across every lease in the lookback window. Also appends [NotAfter] to
-- the requester lookup indexes so the clock check seeks past expired history.
IF COL_LENGTH('[dbo].[AccessLease]', 'SweptDate') IS NULL
BEGIN
    ALTER TABLE [dbo].[AccessLease] ADD [SweptDate] DATETIME2(7) NULL;
END
GO

-- Carries the journal over so already-swept leases are not swept again.
IF OBJECT_ID('[dbo].[PamLeaseExpirySweep]') IS NOT NULL
BEGIN
    UPDATE
        AL
    SET
        AL.[SweptDate] = S.[SweptDate]
    FROM
        [dbo].[AccessLease] AL
    INNER JOIN
        [dbo].[PamLeaseExpirySweep] S ON S.[AccessLeaseId] = AL.[Id]
    WHERE
        AL.[SweptDate] IS NULL
END
GO

IF NOT EXISTS (
    SELECT
        NULL
    FROM
        sys.indexes
    WHERE
        [name] = 'IX_AccessLease_SweptDate_Action_NotAfter'
        AND object_id = OBJECT_ID('[dbo].[AccessLease]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AccessLease_SweptDate_Action_NotAfter]
    ON [dbo].[AccessLease] ([SweptDate] ASC, [Action] ASC, [NotAfter] ASC);
END
GO

IF EXISTS (
    SELECT
        NULL
    FROM
        sys.indexes
    WHERE
        [name] = 'IX_AccessLease_NotAfter_Action'
        AND object_id = OBJECT_ID('[dbo].[AccessLease]')
)
BEGIN
    DROP INDEX [IX_AccessLease_NotAfter_Action] ON [dbo].[AccessLease];
END
GO

IF NOT EXISTS (
    SELECT
        NULL
    FROM
        sys.indexes
    WHERE
        [name] = 'IX_AccessLease_RequesterId_Action_NotAfter'
        AND object_id = OBJECT_ID('[dbo].[AccessLease]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AccessLease_RequesterId_Action_NotAfter]
    ON [dbo].[AccessLease] ([RequesterId] ASC, [Action] ASC, [NotAfter] ASC)
    INCLUDE ([AccessRequestId], [OrganizationId], [CollectionId], [CipherId], [NotBefore], [RevokedDate], [RevokedBy], [CreationDate], [SweptDate]);
END
GO

IF EXISTS (
    SELECT
        NULL
    FROM
        sys.indexes
    WHERE
        [name] = 'IX_AccessLease_RequesterId_CipherId_Action'
        AND object_id = OBJECT_ID('[dbo].[AccessLease]')
)
BEGIN
    DROP INDEX [IX_AccessLease_RequesterId_CipherId_Action] ON [dbo].[AccessLease];
END
GO

IF NOT EXISTS (
    SELECT
        NULL
    FROM
        sys.indexes
    WHERE
        [name] = 'IX_AccessRequest_RequesterId_CipherId_Action_NotAfter'
        AND object_id = OBJECT_ID('[dbo].[AccessRequest]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AccessRequest_RequesterId_CipherId_Action_NotAfter]
    ON [dbo].[AccessRequest] ([RequesterId] ASC, [CipherId] ASC, [Action] ASC, [NotAfter] ASC)
    INCLUDE ([ExtensionOfLeaseId], [CreationDate]);
END
GO

IF EXISTS (
    SELECT
        NULL
    FROM
        sys.indexes
    WHERE
        [name] = 'IX_AccessRequest_RequesterId_CipherId_Action'
        AND object_id = OBJECT_ID('[dbo].[AccessRequest]')
)
BEGIN
    DROP INDEX [IX_AccessRequest_RequesterId_CipherId_Action] ON [dbo].[AccessRequest];
END
GO

CREATE OR ALTER PROCEDURE [dbo].[PamLeaseExpirySweep_CreateManyDue]
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
GO

-- Second pass: a sweep still running the previous procedure may have journaled leases since the first.
IF OBJECT_ID('[dbo].[PamLeaseExpirySweep]') IS NOT NULL
BEGIN
    UPDATE
        AL
    SET
        AL.[SweptDate] = S.[SweptDate]
    FROM
        [dbo].[AccessLease] AL
    INNER JOIN
        [dbo].[PamLeaseExpirySweep] S ON S.[AccessLeaseId] = AL.[Id]
    WHERE
        AL.[SweptDate] IS NULL
END
GO

IF OBJECT_ID('[dbo].[PamLeaseExpirySweep]') IS NOT NULL
BEGIN
    DROP TABLE [dbo].[PamLeaseExpirySweep];
END
GO
