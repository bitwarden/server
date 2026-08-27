CREATE TABLE [dbo].[AccessRequest] (
    [Id]                    UNIQUEIDENTIFIER    NOT NULL,
    [ExtensionOfLeaseId]    UNIQUEIDENTIFIER    NULL,
    [OrganizationId]        UNIQUEIDENTIFIER    NOT NULL,
    [CollectionId]          UNIQUEIDENTIFIER    NOT NULL,
    [CipherId]              UNIQUEIDENTIFIER    NOT NULL,
    [RequesterId]           UNIQUEIDENTIFIER    NOT NULL,
    [NotBefore]             DATETIME2 (7)       NOT NULL,
    [NotAfter]              DATETIME2 (7)       NOT NULL,
    [Reason]                NVARCHAR(MAX)       NULL,
    -- The action a party has taken on the request (AccessRequestAction: 0 None, 1 Approved, 2 Denied, 3 Cancelled).
    -- A recorded fact, never a claim about the present: Pending and Expired are derived against the read clock and
    -- are unrepresentable here. [ActionDate] is when the current action was recorded (NULL iff [Action] = 0).
    [Action]                TINYINT             NOT NULL,
    [CreationDate]          DATETIME2 (7)       NOT NULL,
    [ActionDate]            DATETIME2 (7)       NULL,
    [RuleId]                UNIQUEIDENTIFIER    NULL,
    CONSTRAINT [PK_AccessRequest] PRIMARY KEY CLUSTERED ([Id] ASC),
    CONSTRAINT [FK_AccessRequest_AccessLease] FOREIGN KEY ([ExtensionOfLeaseId]) REFERENCES [dbo].[AccessLease] ([Id]),
    CONSTRAINT [FK_AccessRequest_AccessRule] FOREIGN KEY ([RuleId]) REFERENCES [dbo].[AccessRule] ([Id]),
    CONSTRAINT [FK_AccessRequest_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]) ON DELETE CASCADE
);
GO

CREATE NONCLUSTERED INDEX [IX_AccessRequest_RequesterId_CipherId_Action]
    ON [dbo].[AccessRequest] ([RequesterId] ASC, [CipherId] ASC, [Action] ASC);
GO

CREATE NONCLUSTERED INDEX [IX_AccessRequest_OrganizationId_Action]
    ON [dbo].[AccessRequest] ([OrganizationId] ASC, [Action] ASC);
GO

-- Supports the approver's pending inbox (AccessRequest_ReadInboxPendingByCollectionIds -- the hottest PAM read: the
-- nav badge and the inbox itself, refreshed on every access push), which joins the caller's manageable collection
-- ids and asks for [Action] = 0 with a window still open. [NotAfter] is a key column rather than an INCLUDE so the
-- clock comparison seeks: under derived status nothing ever writes Expired, so lapsed unanswered rows stay at
-- [Action] = 0 and accumulate without bound -- off the key, every one of them would be matched and fetched only to
-- be discarded. Mirrors IX_AccessLease_CollectionId_Action for the same governance-by-collection access pattern.
CREATE NONCLUSTERED INDEX [IX_AccessRequest_CollectionId_Action_NotAfter]
    ON [dbo].[AccessRequest] ([CollectionId] ASC, [Action] ASC, [NotAfter] ASC);
GO

-- Supports the approver history (AccessRequest_ReadInboxHistoryByCollectionIds). Its non-actionable test
-- ([Action] <> 0 OR [NotAfter] <= @Now) ORs across two columns and cannot seek, which leaves the retention bound
-- @Since as the only predicate able to keep the read small -- so [CreationDate] leads here and does that work.
-- [Action] and [NotAfter] are INCLUDEd so the OR is settled from the index, and only the rows that survive it cost
-- a clustered lookup.
CREATE NONCLUSTERED INDEX [IX_AccessRequest_CollectionId_CreationDate]
    ON [dbo].[AccessRequest] ([CollectionId] ASC, [CreationDate] ASC)
    INCLUDE ([Action], [NotAfter]);
GO

-- The requester's own history (AccessRequest_ReadManyByRequesterId): TOP (250) ORDER BY [CreationDate] DESC over one
-- requester, past the same shape of action/clock OR -- there exempting live rows from the @Since window.
-- [IX_AccessRequest_RequesterId_CipherId_Action] cannot serve it: [CipherId] sits between the requester and any
-- ordering, so the page costs a sort of that requester's entire history. Keyed [CreationDate] instead, the page
-- comes off a backward ordered scan that stops at 250, with [Action]/[NotAfter] INCLUDEd to settle the exemption
-- before the lookup.
CREATE NONCLUSTERED INDEX [IX_AccessRequest_RequesterId_CreationDate]
    ON [dbo].[AccessRequest] ([RequesterId] ASC, [CreationDate] ASC)
    INCLUDE ([Action], [NotAfter]);
GO

-- Supports the extension cap checks (AccessRequest_CreateApprovedExtension's EXISTS, which runs while holding the
-- parent lease's UPDLOCK, and AccessRequest_CountExtensionsByLeaseId), and indexes FK_AccessRequest_AccessLease.
CREATE NONCLUSTERED INDEX [IX_AccessRequest_ExtensionOfLeaseId]
    ON [dbo].[AccessRequest] ([ExtensionOfLeaseId] ASC);
GO

-- Indexes FK_AccessRequest_AccessRule so AccessRule_DeleteById can detach the requests pinning a rule with a seek
-- rather than a scan.
CREATE NONCLUSTERED INDEX [IX_AccessRequest_RuleId]
    ON [dbo].[AccessRequest] ([RuleId] ASC);
GO
