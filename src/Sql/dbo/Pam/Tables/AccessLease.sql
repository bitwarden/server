CREATE TABLE [dbo].[AccessLease] (
    [Id]                 UNIQUEIDENTIFIER    NOT NULL,
    [AccessRequestId]    UNIQUEIDENTIFIER    NOT NULL,
    [OrganizationId]    UNIQUEIDENTIFIER    NOT NULL,
    [CollectionId]      UNIQUEIDENTIFIER    NOT NULL,
    [CipherId]          UNIQUEIDENTIFIER    NOT NULL,
    [RequesterId]       UNIQUEIDENTIFIER    NOT NULL,
    -- How the lease was ended early, if it was (AccessLeaseAction: 0 None, 2 Revoked, 3 Cancelled; byte 1, the old
    -- stored Expired, is retired). A recorded fact, never a claim about the present: Active and Expired are derived
    -- against the read clock and are unrepresentable here. The happy-path lease carries 0 forever.
    [Action]            TINYINT             NOT NULL,
    [NotBefore]         DATETIME2 (7)       NOT NULL,
    [NotAfter]          DATETIME2 (7)       NOT NULL,
    [RevokedDate]       DATETIME2 (7)       NULL,
    [RevokedBy]         UNIQUEIDENTIFIER    NULL,
    [CreationDate]      DATETIME2 (7)       NOT NULL,
    CONSTRAINT [PK_AccessLease] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_AccessLease_AccessRequest] FOREIGN KEY ([AccessRequestId]) REFERENCES [dbo].[AccessRequest] ([Id]),
    CONSTRAINT [FK_AccessLease_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]) ON DELETE CASCADE
);
GO

CREATE NONCLUSTERED INDEX [IX_AccessLease_RequesterId_CipherId_Action]
    ON [dbo].[AccessLease] ([RequesterId] ASC, [CipherId] ASC, [Action] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_AccessLease_NotAfter_Action]
    ON [dbo].[AccessLease] ([NotAfter] ASC, [Action] ASC);
GO

-- Supports the governance lease lists (AccessLease_ReadManyActiveByCollectionIds /
-- AccessLease_ReadManyEndedByCollectionIds), which filter by the caller's manageable collection ids.
CREATE NONCLUSTERED INDEX [IX_AccessLease_CollectionId_Action]
    ON [dbo].[AccessLease] ([CollectionId] ASC, [Action] ASC);
GO

-- Supports the per-cipher singleton guard in AccessLease_CreateFromApprovedRequest. That guard filters on CipherId
-- alone under UPDLOCK/HOLDLOCK, so without a CipherId-leading index the range lock it takes covers either the whole
-- table or every currently-running and future lease, serializing unrelated organizations' activations against it.
--
-- NotAfter DESC is the third key so AccessLease_ReadActiveByCipherId (the pre-check's "is the slot taken, and when
-- does it free") seeks straight to the in-window rows in the order it wants them. On (CipherId, Action) alone that
-- read has to look up every Action = 0 row for the cipher -- its whole happy-path lease history, since an untouched
-- lease keeps Action = 0 forever -- and then sort. The guard is unaffected: its predicate uses the same two leading
-- columns.
CREATE NONCLUSTERED INDEX [IX_AccessLease_CipherId_Action]
    ON [dbo].[AccessLease] ([CipherId] ASC, [Action] ASC, [NotAfter] DESC);
GO

-- A request produces at most one lease, ever: activating an approved request and the automatic path each insert
-- exactly one. Unique to backstop racing activations that pass the application-level checks simultaneously.
CREATE UNIQUE NONCLUSTERED INDEX [IX_AccessLease_AccessRequestId]
    ON [dbo].[AccessLease] ([AccessRequestId] ASC);
GO
