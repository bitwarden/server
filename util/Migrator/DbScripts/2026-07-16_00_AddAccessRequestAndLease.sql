-- Add the PAM Access Requests + Leases feature: the AccessRequest, AccessLease, and AccessDecision tables and their
-- stored procedures. Consolidated net-new migration (the feature has not shipped).
--
-- AccessRequest and AccessLease have a circular FK relationship (AccessRequest.ExtensionOfLeaseId -> AccessLease.Id,
-- AccessLease.AccessRequestId -> AccessRequest.Id), so AccessRequest is created first without the FK to AccessLease,
-- and that FK is added via ALTER TABLE once AccessLease exists.

-- AccessRequest (created first; the FK to AccessLease is added later, once AccessLease exists).
IF OBJECT_ID('[dbo].[AccessRequest]') IS NULL
BEGIN
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
        [Status]                TINYINT             NOT NULL,
        [CreationDate]          DATETIME2 (7)       NOT NULL,
        [ResolvedDate]          DATETIME2 (7)       NULL,
        [RuleId]                UNIQUEIDENTIFIER    NULL,
        CONSTRAINT [PK_AccessRequest] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_AccessRequest_AccessRule] FOREIGN KEY ([RuleId]) REFERENCES [dbo].[AccessRule] ([Id]),
        CONSTRAINT [FK_AccessRequest_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]) ON DELETE CASCADE
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_AccessRequest_RequesterId_CipherId_Status' AND object_id = OBJECT_ID('[dbo].[AccessRequest]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AccessRequest_RequesterId_CipherId_Status]
        ON [dbo].[AccessRequest] ([RequesterId] ASC, [CipherId] ASC, [Status] ASC);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_AccessRequest_OrganizationId_Status' AND object_id = OBJECT_ID('[dbo].[AccessRequest]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AccessRequest_OrganizationId_Status]
        ON [dbo].[AccessRequest] ([OrganizationId] ASC, [Status] ASC);
END
GO

-- AccessLease (AccessRequest already exists, so its FK can be included directly).
IF OBJECT_ID('[dbo].[AccessLease]') IS NULL
BEGIN
    CREATE TABLE [dbo].[AccessLease] (
        [Id]                 UNIQUEIDENTIFIER    NOT NULL,
        [AccessRequestId]    UNIQUEIDENTIFIER    NOT NULL,
        [OrganizationId]     UNIQUEIDENTIFIER    NOT NULL,
        [CollectionId]       UNIQUEIDENTIFIER    NOT NULL,
        [CipherId]           UNIQUEIDENTIFIER    NOT NULL,
        [RequesterId]        UNIQUEIDENTIFIER    NOT NULL,
        [Status]             TINYINT             NOT NULL,
        [NotBefore]          DATETIME2 (7)       NOT NULL,
        [NotAfter]           DATETIME2 (7)       NOT NULL,
        [RevokedDate]        DATETIME2 (7)       NULL,
        [RevokedBy]          UNIQUEIDENTIFIER    NULL,
        [CreationDate]       DATETIME2 (7)       NOT NULL,
        CONSTRAINT [PK_AccessLease] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_AccessLease_AccessRequest] FOREIGN KEY ([AccessRequestId]) REFERENCES [dbo].[AccessRequest] ([Id]),
        CONSTRAINT [FK_AccessLease_Organization] FOREIGN KEY ([OrganizationId]) REFERENCES [dbo].[Organization] ([Id]) ON DELETE CASCADE
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_AccessLease_RequesterId_CipherId_Status' AND object_id = OBJECT_ID('[dbo].[AccessLease]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AccessLease_RequesterId_CipherId_Status]
        ON [dbo].[AccessLease] ([RequesterId] ASC, [CipherId] ASC, [Status] ASC);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_AccessLease_NotAfter_Status' AND object_id = OBJECT_ID('[dbo].[AccessLease]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AccessLease_NotAfter_Status]
        ON [dbo].[AccessLease] ([NotAfter] ASC, [Status] ASC);
END
GO

-- Supports the governance lease lists (AccessLease_ReadManyActiveByCollectionIds /
-- AccessLease_ReadManyEndedByCollectionIds), which filter by the caller's manageable collection ids.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_AccessLease_CollectionId_Status' AND object_id = OBJECT_ID('[dbo].[AccessLease]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AccessLease_CollectionId_Status]
        ON [dbo].[AccessLease] ([CollectionId] ASC, [Status] ASC);
END
GO

-- A request produces at most one lease, ever: activating an approved request and the automatic path each insert
-- exactly one. Unique to backstop racing activations that pass the application-level checks simultaneously.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_AccessLease_AccessRequestId' AND object_id = OBJECT_ID('[dbo].[AccessLease]'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX [IX_AccessLease_AccessRequestId]
        ON [dbo].[AccessLease] ([AccessRequestId] ASC);
END
GO

-- Now that AccessLease exists, add the reciprocal FK from AccessRequest.ExtensionOfLeaseId.
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE [name] = 'FK_AccessRequest_AccessLease')
BEGIN
    ALTER TABLE [dbo].[AccessRequest]
        ADD CONSTRAINT [FK_AccessRequest_AccessLease] FOREIGN KEY ([ExtensionOfLeaseId]) REFERENCES [dbo].[AccessLease] ([Id]);
END
GO

-- AccessDecision
IF OBJECT_ID('[dbo].[AccessDecision]') IS NULL
BEGIN
    CREATE TABLE [dbo].[AccessDecision] (
        [Id]                    UNIQUEIDENTIFIER    NOT NULL,
        [AccessRequestId]       UNIQUEIDENTIFIER    NOT NULL,
        [DeciderKind]           TINYINT             NOT NULL,
        [ApproverId]            UNIQUEIDENTIFIER    NULL,
        [ConditionKind]         TINYINT             NULL,
        [Verdict]               TINYINT             NOT NULL,
        [Comment]               NVARCHAR(MAX)       NULL,
        [EvaluationContext]     NVARCHAR(MAX)       NULL,
        [CreationDate]          DATETIME2 (7)       NOT NULL,
        CONSTRAINT [PK_AccessDecision] PRIMARY KEY CLUSTERED ([Id] ASC),
        CONSTRAINT [FK_AccessDecision_AccessRequest] FOREIGN KEY ([AccessRequestId]) REFERENCES [dbo].[AccessRequest] ([Id]) ON DELETE CASCADE
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_AccessDecision_AccessRequestId' AND object_id = OBJECT_ID('[dbo].[AccessDecision]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AccessDecision_AccessRequestId]
        ON [dbo].[AccessDecision] ([AccessRequestId] ASC);
END
GO

-- Stored procedures

-- AccessLease_CreateFromApprovedRequest
CREATE OR ALTER PROCEDURE [dbo].[AccessLease_CreateFromApprovedRequest]
    @AccessLeaseId UNIQUEIDENTIFIER,
    @AccessRequestId UNIQUEIDENTIFIER,
    @RequesterId UNIQUEIDENTIFIER,
    @Now DATETIME2(7),
    @EnforceSingleActiveLease BIT = 0
AS
BEGIN
    SET NOCOUNT ON
    -- An explicit transaction is required so the singleton guard's range lock is held until the INSERT commits;
    -- XACT_ABORT guarantees the transaction is rolled back (and the pooled connection left clean) if the
    -- unique-index backstop [IX_AccessLease_AccessRequestId] trips on a concurrent activation of the same request.
    SET XACT_ABORT ON

    BEGIN TRANSACTION

    -- Per-cipher singleton guard. When the governing rule(s) ask for a single active lease, activation is allowed
    -- only if no other active in-window lease exists for the same cipher across all users. The UPDLOCK, HOLDLOCK
    -- range lock is held for the life of this transaction, so it serializes against the INSERT below: a concurrent
    -- same-cipher activation blocks here until this transaction commits, then sees the new lease and is rejected.
    -- Outcome -1 is distinct from the precondition-fail outcome (0) so the caller can surface a 409 conflict.
    IF @EnforceSingleActiveLease = 1
        AND EXISTS (
            SELECT 1
            FROM [dbo].[AccessLease] WITH (UPDLOCK, HOLDLOCK)
            WHERE [CipherId] = (SELECT [CipherId] FROM [dbo].[AccessRequest] WHERE [Id] = @AccessRequestId)
                AND [Status] = 0 /* Active */
                AND [NotBefore] <= @Now
                AND [NotAfter] > @Now
        )
    BEGIN
        ROLLBACK TRANSACTION
        SELECT -1
        RETURN
    END

    -- Activation of an approved request: mints the active lease that authorizes access, spanning the request's
    -- approved window. Every application-level precondition is re-checked inside the INSERT so a concurrent
    -- activation cannot double-mint; zero rows inserted means a precondition no longer held and the caller decides
    -- how to surface that. [IX_AccessLease_AccessRequestId] (unique) is the backstop when two calls pass the
    -- NOT EXISTS check simultaneously.
    INSERT INTO [dbo].[AccessLease]
    (
        [Id], [AccessRequestId], [OrganizationId], [CollectionId], [CipherId], [RequesterId],
        [Status], [NotBefore], [NotAfter], [RevokedDate], [RevokedBy], [CreationDate]
    )
    SELECT
        @AccessLeaseId, AR.[Id], AR.[OrganizationId], AR.[CollectionId], AR.[CipherId], AR.[RequesterId],
        0 /* Active */, AR.[NotBefore], AR.[NotAfter], NULL, NULL, @Now
    FROM [dbo].[AccessRequest] AR
    WHERE
        AR.[Id] = @AccessRequestId
        AND AR.[RequesterId] = @RequesterId
        AND AR.[Status] = 1 -- Approved
        AND AR.[NotBefore] <= @Now
        AND AR.[NotAfter] > @Now
        AND NOT EXISTS (SELECT 1 FROM [dbo].[AccessLease] AL WHERE AL.[AccessRequestId] = AR.[Id])

    DECLARE @Rows INT = @@ROWCOUNT

    COMMIT TRANSACTION

    -- 1 = minted, 0 = precondition no longer held (caller re-reads the winner).
    SELECT CASE WHEN @Rows = 1 THEN 1 ELSE 0 END
END
GO

-- AccessLease_ReadActiveByRequesterIdCipherId
CREATE OR ALTER PROCEDURE [dbo].[AccessLease_ReadActiveByRequesterIdCipherId]
    @RequesterId UNIQUEIDENTIFIER,
    @CipherId UNIQUEIDENTIFIER,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    SELECT TOP 1
        *
    FROM
        [dbo].[AccessLease]
    WHERE
        [RequesterId] = @RequesterId
        AND [CipherId] = @CipherId
        AND [Status] = 0 -- Active
        AND [NotBefore] <= @Now
        AND [NotAfter] > @Now
    ORDER BY
        [NotAfter] DESC
END
GO

-- AccessLease_ReadByAccessRequestId
CREATE OR ALTER PROCEDURE [dbo].[AccessLease_ReadByAccessRequestId]
    @AccessRequestId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    -- A request produces at most one lease ([IX_AccessLease_AccessRequestId] is unique); TOP 1 is belt and braces.
    SELECT TOP 1
        *
    FROM
        [dbo].[AccessLease]
    WHERE
        [AccessRequestId] = @AccessRequestId
    ORDER BY
        [CreationDate] DESC
END
GO

-- AccessLease_ReadById
CREATE OR ALTER PROCEDURE [dbo].[AccessLease_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[AccessLease]
    WHERE
        [Id] = @Id
END
GO

-- AccessLease_ReadManyActiveByCollectionIds
CREATE OR ALTER PROCEDURE [dbo].[AccessLease_ReadManyActiveByCollectionIds]
    @CollectionIds [dbo].[GuidIdArray] READONLY,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- Governance view: every currently-active lease (Active, window containing @Now) on the supplied
    -- (caller-manageable) collections, across all members -- not just the caller's own.
    SELECT
        L.*
    FROM
        [dbo].[AccessLease] L
        INNER JOIN @CollectionIds CI ON CI.[Id] = L.[CollectionId]
    WHERE
        L.[Status] = 0 -- Active
        AND L.[NotBefore] <= @Now
        AND L.[NotAfter] > @Now
    ORDER BY
        L.[NotAfter] ASC
END
GO

-- AccessLease_ReadManyActiveByRequesterId
CREATE OR ALTER PROCEDURE [dbo].[AccessLease_ReadManyActiveByRequesterId]
    @RequesterId UNIQUEIDENTIFIER,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[AccessLease]
    WHERE
        [RequesterId] = @RequesterId
        AND [Status] = 0 -- Active
        AND [NotBefore] <= @Now
        AND [NotAfter] > @Now
    ORDER BY
        [NotAfter] ASC
END
GO

-- AccessLease_ReadManyEndedByCollectionIds
CREATE OR ALTER PROCEDURE [dbo].[AccessLease_ReadManyEndedByCollectionIds]
    @CollectionIds [dbo].[GuidIdArray] READONLY,
    @Since DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- Governance history: leases that have ended (Expired, Revoked, or Cancelled) on the supplied (caller-manageable)
    -- collections, that ended on or after @Since. A revoked/cancelled lease's end is its RevokedDate; an expired
    -- lease's end is its NotAfter. Most recently ended first.
    SELECT
        L.*
    FROM
        [dbo].[AccessLease] L
        INNER JOIN @CollectionIds CI ON CI.[Id] = L.[CollectionId]
    WHERE
        L.[Status] IN (1, 2, 3) -- Expired, Revoked, Cancelled
        AND (
            (L.[Status] IN (2, 3) AND L.[RevokedDate] >= @Since) -- Revoked, Cancelled
            OR (L.[Status] = 1 AND L.[NotAfter] >= @Since) -- Expired
        )
    ORDER BY
        CASE WHEN L.[Status] IN (2, 3) THEN L.[RevokedDate] ELSE L.[NotAfter] END DESC
END
GO

-- AccessLease_Revoke
CREATE OR ALTER PROCEDURE [dbo].[AccessLease_Revoke]
    @AccessLeaseId UNIQUEIDENTIFIER,
    @AccessRequestId UNIQUEIDENTIFIER,
    @Status TINYINT,
    @RevokedBy UNIQUEIDENTIFIER,
    @AccessDecisionId UNIQUEIDENTIFIER,
    @Reason NVARCHAR(MAX) = NULL,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- Atomically end an active lease and capture who/why. @Status is the end state: 2 (Revoked) when an operator ended
    -- it, 3 (Cancelled) when the holder ended their own; RevokedDate/RevokedBy record when/who either way. The reason
    -- has no dedicated column, so it is preserved as a human AccessDecision (Deny) against the lease's originating
    -- request, keeping the audit trail without a schema change. The WHERE guard keeps the end idempotent if two
    -- callers race.
    BEGIN TRANSACTION AccessLease_Revoke

    UPDATE [dbo].[AccessLease]
    SET [Status] = @Status,
        [RevokedDate] = @Now,
        [RevokedBy] = @RevokedBy
    WHERE [Id] = @AccessLeaseId AND [Status] = 0 -- Active

    INSERT INTO [dbo].[AccessDecision]
    (
        [Id], [AccessRequestId], [DeciderKind], [ApproverId], [ConditionKind],
        [Verdict], [Comment], [EvaluationContext], [CreationDate]
    )
    VALUES
    (
        @AccessDecisionId, @AccessRequestId, 1 /* Human */, @RevokedBy, NULL,
        0 /* Deny */, @Reason, NULL, @Now
    )

    COMMIT TRANSACTION AccessLease_Revoke
END
GO

-- AccessRequest_Cancel
CREATE OR ALTER PROCEDURE [dbo].[AccessRequest_Cancel]
    @AccessRequestId UNIQUEIDENTIFIER,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- The requester withdraws their own not-yet-activated request (Pending, or an Approved request they have not
    -- activated). Unlike [AccessRequest_CancelWithDecision], no AccessDecision is written: a cancellation is the
    -- requester acting on their own request, not an approver verdict. The WHERE guard keeps the write idempotent under
    -- a race and refuses a request that has already produced a lease (that access is governed by the lease, which must
    -- be revoked instead).
    UPDATE [dbo].[AccessRequest]
    SET [Status] = 3, -- Cancelled
        [ResolvedDate] = @Now
    WHERE [Id] = @AccessRequestId
        AND [Status] IN (0, 1) -- Pending or Approved
        AND NOT EXISTS (SELECT 1 FROM [dbo].[AccessLease] L WHERE L.[AccessRequestId] = @AccessRequestId)
END
GO

-- AccessRequest_CancelWithDecision
CREATE OR ALTER PROCEDURE [dbo].[AccessRequest_CancelWithDecision]
    @AccessRequestId UNIQUEIDENTIFIER,
    @AccessDecisionId UNIQUEIDENTIFIER,
    @ApproverId UNIQUEIDENTIFIER,
    @Verdict TINYINT,
    @Comment NVARCHAR(MAX) = NULL,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- A managing approver retracts a not-yet-activated request (Pending, or an Approved request the requester has not
    -- activated): transition it to Denied and record the approver's human decision, mirroring
    -- [AccessRequest_ResolveWithDecision] but over the broader cancellable set. The WHERE guard is race-safe and
    -- refuses a request that has produced a lease (governed by the lease — revoke instead). The decision is inserted
    -- only when the transition actually happened (@@ROWCOUNT > 0), so a no-op never orphans an AccessDecision.
    BEGIN TRANSACTION AccessRequest_CancelWithDecision

    UPDATE [dbo].[AccessRequest]
    SET [Status] = 2, -- Denied
        [ResolvedDate] = @Now
    WHERE [Id] = @AccessRequestId
        AND [Status] IN (0, 1) -- Pending or Approved
        AND NOT EXISTS (SELECT 1 FROM [dbo].[AccessLease] L WHERE L.[AccessRequestId] = @AccessRequestId)

    IF @@ROWCOUNT > 0
    BEGIN
        INSERT INTO [dbo].[AccessDecision]
        (
            [Id], [AccessRequestId], [DeciderKind], [ApproverId], [ConditionKind],
            [Verdict], [Comment], [EvaluationContext], [CreationDate]
        )
        VALUES
        (
            @AccessDecisionId, @AccessRequestId, 1 /* Human */, @ApproverId, NULL,
            @Verdict, @Comment, NULL, @Now
        )
    END

    COMMIT TRANSACTION AccessRequest_CancelWithDecision
END
GO

-- AccessRequest_CountExtensionsByLeaseId
CREATE OR ALTER PROCEDURE [dbo].[AccessRequest_CountExtensionsByLeaseId]
    @LeaseId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    -- Number of extension requests recorded against the lease. Extensions are always auto-approved, so every such
    -- request counts toward the governing rule's per-lease maximum.
    SELECT COUNT(*)
    FROM [dbo].[AccessRequest]
    WHERE [ExtensionOfLeaseId] = @LeaseId
END
GO

-- AccessRequest_Create
CREATE OR ALTER PROCEDURE [dbo].[AccessRequest_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @ExtensionOfLeaseId UNIQUEIDENTIFIER = NULL,
    @OrganizationId UNIQUEIDENTIFIER,
    @CollectionId UNIQUEIDENTIFIER,
    @CipherId UNIQUEIDENTIFIER,
    @RequesterId UNIQUEIDENTIFIER,
    @NotBefore DATETIME2(7),
    @NotAfter DATETIME2(7),
    @Reason NVARCHAR(MAX) = NULL,
    @Status TINYINT,
    @CreationDate DATETIME2(7),
    @ResolvedDate DATETIME2(7) = NULL,
    @RuleId UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[AccessRequest]
    (
        [Id],
        [ExtensionOfLeaseId],
        [OrganizationId],
        [CollectionId],
        [CipherId],
        [RequesterId],
        [NotBefore],
        [NotAfter],
        [Reason],
        [Status],
        [CreationDate],
        [ResolvedDate],
        [RuleId]
    )
    VALUES
    (
        @Id,
        @ExtensionOfLeaseId,
        @OrganizationId,
        @CollectionId,
        @CipherId,
        @RequesterId,
        @NotBefore,
        @NotAfter,
        @Reason,
        @Status,
        @CreationDate,
        @ResolvedDate,
        @RuleId
    )
END
GO

-- AccessRequest_CreateApprovedExtension
CREATE OR ALTER PROCEDURE [dbo].[AccessRequest_CreateApprovedExtension]
    @AccessRequestId UNIQUEIDENTIFIER,
    @AccessDecisionId UNIQUEIDENTIFIER,
    @ExtensionOfLeaseId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @CollectionId UNIQUEIDENTIFIER,
    @CipherId UNIQUEIDENTIFIER,
    @RequesterId UNIQUEIDENTIFIER,
    @NotBefore DATETIME2(7),
    @NotAfter DATETIME2(7),
    @Reason NVARCHAR(MAX) = NULL,
    @Now DATETIME2(7),
    @RuleId UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON
    -- An explicit transaction holds the per-lease range lock until the writes commit, so concurrent extensions of
    -- the same lease serialize. XACT_ABORT guarantees rollback (and a clean pooled connection) on any error.
    SET XACT_ABORT ON

    BEGIN TRANSACTION

    -- Lock the parent lease row for the life of the transaction. A second concurrent extension of the same lease
    -- blocks here until this transaction commits, then re-counts below and sees this extension. The lease must
    -- still be active and in-window to be extendable; outcome 0 is distinct from the cap conflict (-1).
    IF NOT EXISTS (
        SELECT 1
        FROM [dbo].[AccessLease] WITH (UPDLOCK, HOLDLOCK)
        WHERE [Id] = @ExtensionOfLeaseId
            AND [RequesterId] = @RequesterId
            AND [Status] = 0 /* Active */
            AND [NotAfter] > @Now
    )
    BEGIN
        ROLLBACK TRANSACTION
        SELECT 0 -- LeaseNotActive
        RETURN
    END

    -- A lease may be extended exactly once. Counted under the lease lock, so it is race-safe against a concurrent
    -- extension of the same lease.
    IF EXISTS (SELECT 1 FROM [dbo].[AccessRequest] WHERE [ExtensionOfLeaseId] = @ExtensionOfLeaseId)
    BEGIN
        ROLLBACK TRANSACTION
        SELECT -1 -- AlreadyExtended
        RETURN
    END

    -- Record the auto-approved extension request and its automatic verdict, then push the parent lease's end out in
    -- place. No new lease is minted — extending reuses the existing lease, preserving the single-active-lease
    -- invariant. The request's window spans the extension ([old lease end] .. [new lease end]); its NotAfter is the
    -- lease's new end.
    INSERT INTO [dbo].[AccessRequest]
    (
        [Id], [ExtensionOfLeaseId], [OrganizationId], [CollectionId], [CipherId], [RequesterId],
        [NotBefore], [NotAfter], [Reason], [Status], [CreationDate], [ResolvedDate], [RuleId]
    )
    VALUES
    (
        @AccessRequestId, @ExtensionOfLeaseId, @OrganizationId, @CollectionId, @CipherId, @RequesterId,
        @NotBefore, @NotAfter, @Reason, 1 /* Approved */, @Now, @Now, @RuleId
    )

    INSERT INTO [dbo].[AccessDecision]
    (
        [Id], [AccessRequestId], [DeciderKind], [ApproverId], [ConditionKind],
        [Verdict], [Comment], [EvaluationContext], [CreationDate]
    )
    VALUES
    (
        @AccessDecisionId, @AccessRequestId, 0 /* Automatic */, NULL, NULL,
        1 /* Approve */, NULL, NULL, @Now
    )

    UPDATE [dbo].[AccessLease]
    SET [NotAfter] = @NotAfter
    WHERE [Id] = @ExtensionOfLeaseId

    COMMIT TRANSACTION

    SELECT 1 -- Extended
END
GO

-- AccessRequest_CreateAutoApproved
CREATE OR ALTER PROCEDURE [dbo].[AccessRequest_CreateAutoApproved]
    @AccessRequestId UNIQUEIDENTIFIER,
    @AccessDecisionId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @CollectionId UNIQUEIDENTIFIER,
    @CipherId UNIQUEIDENTIFIER,
    @RequesterId UNIQUEIDENTIFIER,
    @NotBefore DATETIME2(7),
    @NotAfter DATETIME2(7),
    @Reason NVARCHAR(MAX) = NULL,
    @ConditionKind TINYINT = NULL,
    @CreationDate DATETIME2(7),
    @RuleId UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON

    -- Atomically record an auto-approved request and its automatic verdict. No lease is minted here: the requester
    -- activates the approved request later via [AccessLease_CreateFromApprovedRequest], exactly like the human path
    -- after approval. The per-cipher single-active-lease guard therefore lives entirely on that activation path.
    BEGIN TRANSACTION AccessRequest_CreateAutoApproved

    -- The request is created already resolved (Approved). ExtensionOfLeaseId stays NULL: it is reserved for extension
    -- requests; provenance for an original lease flows the other way, via AccessLease.AccessRequestId.
    INSERT INTO [dbo].[AccessRequest]
    (
        [Id], [ExtensionOfLeaseId], [OrganizationId], [CollectionId], [CipherId], [RequesterId],
        [NotBefore], [NotAfter], [Reason], [Status], [CreationDate], [ResolvedDate], [RuleId]
    )
    VALUES
    (
        @AccessRequestId, NULL, @OrganizationId, @CollectionId, @CipherId, @RequesterId,
        @NotBefore, @NotAfter, @Reason, 1 /* Approved */, @CreationDate, @CreationDate, @RuleId
    )

    INSERT INTO [dbo].[AccessDecision]
    (
        [Id], [AccessRequestId], [DeciderKind], [ApproverId], [ConditionKind],
        [Verdict], [Comment], [EvaluationContext], [CreationDate]
    )
    VALUES
    (
        @AccessDecisionId, @AccessRequestId, 0 /* Automatic */, NULL, @ConditionKind,
        1 /* Approve */, NULL, NULL, @CreationDate
    )

    COMMIT TRANSACTION AccessRequest_CreateAutoApproved
END
GO

-- AccessRequest_ReadActiveApprovedByRequesterIdCipherId
CREATE OR ALTER PROCEDURE [dbo].[AccessRequest_ReadActiveApprovedByRequesterIdCipherId]
    @RequesterId UNIQUEIDENTIFIER,
    @CipherId UNIQUEIDENTIFIER,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- The caller's approved-but-not-yet-activated request whose window can still produce access. Future windows are
    -- included (the client shows the upcoming window); lapsed windows are excluded so the client never offers an
    -- activation that the server would reject. A request that has produced a lease is activated, not approved.
    -- Extension requests are excluded: an approved extension pushes its parent lease's end out in place and never
    -- produces a lease of its own, so it must not surface here as an activatable "Start access" request.
    SELECT TOP 1
        AR.*
    FROM
        [dbo].[AccessRequest] AR
    WHERE
        AR.[RequesterId] = @RequesterId
        AND AR.[CipherId] = @CipherId
        AND AR.[Status] = 1 -- Approved
        AND AR.[NotAfter] > @Now
        AND AR.[ExtensionOfLeaseId] IS NULL
        AND NOT EXISTS (SELECT 1 FROM [dbo].[AccessLease] AL WHERE AL.[AccessRequestId] = AR.[Id])
    ORDER BY
        AR.[CreationDate] DESC
END
GO

-- AccessRequest_ReadActivePendingByRequesterIdCipherId
CREATE OR ALTER PROCEDURE [dbo].[AccessRequest_ReadActivePendingByRequesterIdCipherId]
    @RequesterId UNIQUEIDENTIFIER,
    @CipherId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT TOP 1
        *
    FROM
        [dbo].[AccessRequest]
    WHERE
        [RequesterId] = @RequesterId
        AND [CipherId] = @CipherId
        AND [Status] = 0 -- Pending
    ORDER BY
        [CreationDate] DESC
END
GO

-- AccessRequest_ReadById
CREATE OR ALTER PROCEDURE [dbo].[AccessRequest_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[AccessRequest]
    WHERE
        [Id] = @Id
END
GO

-- AccessRequest_ReadDetailsById
CREATE OR ALTER PROCEDURE [dbo].[AccessRequest_ReadDetailsById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    -- A single access request projected for the dedicated request page, returned as two result sets so the caller can
    -- attach the request's full decision list without an N+1:
    --   1) the request row with the denormalized requester identity. A row that produced a lease carries
    --      ProducedLeaseId/ProducedLeaseStatus so the client can show (and gate) lease actions.
    --   2) every decision (human or automatic) for the request, keyed by AccessRequestId and ordered oldest-first;
    --      DeciderKind says which, and a human decision's identity is denormalized from [User].
    -- Authorization (requester or managing approver) is enforced by the caller, not this read.
    SELECT
        LR.[Id],
        LR.[ExtensionOfLeaseId],
        LR.[OrganizationId],
        LR.[CollectionId],
        LR.[CipherId],
        LR.[RequesterId],
        LR.[NotBefore],
        LR.[NotAfter],
        LR.[Reason],
        LR.[Status],
        LR.[CreationDate],
        LR.[ResolvedDate],
        LR.[RuleId],
        PL.[Id] AS [ProducedLeaseId],
        PL.[Status] AS [ProducedLeaseStatus],
        U.[Name] AS [RequesterName],
        U.[Email] AS [RequesterEmail]
    FROM [dbo].[AccessRequest] LR
    LEFT JOIN [dbo].[User] U ON U.[Id] = LR.[RequesterId]
    OUTER APPLY (
        SELECT TOP 1 L.[Id], L.[Status]
        FROM [dbo].[AccessLease] L
        WHERE L.[AccessRequestId] = LR.[Id]
        ORDER BY L.[CreationDate] DESC
    ) PL
    WHERE LR.[Id] = @Id

    SELECT
        AD.[AccessRequestId],
        AD.[DeciderKind] AS [DeciderKind],
        AD.[ApproverId] AS [Id],
        AU.[Name] AS [Name],
        AU.[Email] AS [Email],
        AD.[Comment] AS [Comment],
        AD.[Verdict] AS [Verdict],
        AD.[CreationDate] AS [DecidedAt]
    FROM [dbo].[AccessDecision] AD
    LEFT JOIN [dbo].[User] AU ON AU.[Id] = AD.[ApproverId]
    WHERE AD.[AccessRequestId] = @Id
    ORDER BY AD.[CreationDate] ASC
END
GO

-- AccessRequest_ReadInboxHistoryByCollectionIds
CREATE OR ALTER PROCEDURE [dbo].[AccessRequest_ReadInboxHistoryByCollectionIds]
    @CollectionIds [dbo].[GuidIdArray] READONLY,
    @Since DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- The approver history, returned as two result sets so the caller can attach each request's full decision list
    -- without an N+1:
    --   1) the resolved requests (anything no longer Pending) created on or after @Since, for the supplied
    --      (caller-manageable) collections, with the denormalized requester identity. Rows that produced a lease carry
    --      ProducedLeaseId/ProducedLeaseStatus so the client can target (and gate) the Revoke action.
    --   2) every decision (human or automatic) for those requests, keyed by AccessRequestId and ordered oldest-first;
    --      DeciderKind says which, and a human decision's identity is denormalized from [User].
    SELECT
        LR.[Id],
        LR.[ExtensionOfLeaseId],
        LR.[OrganizationId],
        LR.[CollectionId],
        LR.[CipherId],
        LR.[RequesterId],
        LR.[NotBefore],
        LR.[NotAfter],
        LR.[Reason],
        LR.[Status],
        LR.[CreationDate],
        LR.[ResolvedDate],
        LR.[RuleId],
        PL.[Id] AS [ProducedLeaseId],
        PL.[Status] AS [ProducedLeaseStatus],
        U.[Name] AS [RequesterName],
        U.[Email] AS [RequesterEmail]
    FROM [dbo].[AccessRequest] LR
    INNER JOIN @CollectionIds CI ON CI.[Id] = LR.[CollectionId]
    LEFT JOIN [dbo].[User] U ON U.[Id] = LR.[RequesterId]
    OUTER APPLY (
        SELECT TOP 1 L.[Id], L.[Status]
        FROM [dbo].[AccessLease] L
        WHERE L.[AccessRequestId] = LR.[Id]
        ORDER BY L.[CreationDate] DESC
    ) PL
    WHERE LR.[Status] <> 0 -- not Pending
        AND LR.[CreationDate] >= @Since

    SELECT
        AD.[AccessRequestId],
        AD.[DeciderKind] AS [DeciderKind],
        AD.[ApproverId] AS [Id],
        AU.[Name] AS [Name],
        AU.[Email] AS [Email],
        AD.[Comment] AS [Comment],
        AD.[Verdict] AS [Verdict],
        AD.[CreationDate] AS [DecidedAt]
    FROM [dbo].[AccessDecision] AD
    INNER JOIN [dbo].[AccessRequest] LR ON LR.[Id] = AD.[AccessRequestId]
    INNER JOIN @CollectionIds CI ON CI.[Id] = LR.[CollectionId]
    LEFT JOIN [dbo].[User] AU ON AU.[Id] = AD.[ApproverId]
    WHERE LR.[Status] <> 0 -- not Pending
        AND LR.[CreationDate] >= @Since
    ORDER BY AD.[AccessRequestId], AD.[CreationDate] ASC
END
GO

-- AccessRequest_ReadInboxPendingByCollectionIds
CREATE OR ALTER PROCEDURE [dbo].[AccessRequest_ReadInboxPendingByCollectionIds]
    @CollectionIds [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    -- The approver inbox: pending requests for the supplied (caller-manageable) collections, joined with the
    -- denormalized requester identity the client needs so it avoids an N+1. A pending request has not been decided by
    -- anyone yet, so it carries no approvers (the caller leaves the request's approvers list empty); only the resolved
    -- reads return a second decision result set.
    SELECT
        LR.[Id],
        LR.[ExtensionOfLeaseId],
        LR.[OrganizationId],
        LR.[CollectionId],
        LR.[CipherId],
        LR.[RequesterId],
        LR.[NotBefore],
        LR.[NotAfter],
        LR.[Reason],
        LR.[Status],
        LR.[CreationDate],
        LR.[ResolvedDate],
        LR.[RuleId],
        PL.[Id] AS [ProducedLeaseId],
        PL.[Status] AS [ProducedLeaseStatus],
        U.[Name] AS [RequesterName],
        U.[Email] AS [RequesterEmail]
    FROM [dbo].[AccessRequest] LR
    INNER JOIN @CollectionIds CI ON CI.[Id] = LR.[CollectionId]
    LEFT JOIN [dbo].[User] U ON U.[Id] = LR.[RequesterId]
    OUTER APPLY (
        SELECT TOP 1 L.[Id], L.[Status]
        FROM [dbo].[AccessLease] L
        WHERE L.[AccessRequestId] = LR.[Id]
        ORDER BY L.[CreationDate] DESC
    ) PL
    WHERE LR.[Status] = 0 -- Pending
END
GO

-- AccessRequest_ReadManyByRequesterId
CREATE OR ALTER PROCEDURE [dbo].[AccessRequest_ReadManyByRequesterId]
    @RequesterId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    -- The caller's own requests, returned as two result sets so the caller can attach each request's decision list
    -- without an N+1:
    --   1) the caller's requests (TOP 250 most recent), all statuses. Unlike the approver-inbox reads this is a
    --      caller-scoped self-read, so the cipher/collection/requester display-name joins are intentionally omitted
    --      (those names come from the caller's local vault, and the requester is the caller).
    --   2) every decision (human or automatic) on the caller's requests, keyed by AccessRequestId and ordered
    --      oldest-first; DeciderKind says which, and a human decision's identity is denormalized from [User] -- the
    --      requester has no other way to name who decided their request.
    SELECT TOP (250)
        LR.[Id],
        LR.[ExtensionOfLeaseId],
        LR.[OrganizationId],
        LR.[CollectionId],
        LR.[CipherId],
        LR.[RequesterId],
        LR.[NotBefore],
        LR.[NotAfter],
        LR.[Reason],
        LR.[Status],
        LR.[CreationDate],
        LR.[ResolvedDate],
        LR.[RuleId],
        PL.[Id] AS [ProducedLeaseId],
        PL.[Status] AS [ProducedLeaseStatus]
    FROM [dbo].[AccessRequest] LR
    OUTER APPLY (
        SELECT TOP 1 L.[Id], L.[Status]
        FROM [dbo].[AccessLease] L
        WHERE L.[AccessRequestId] = LR.[Id]
        ORDER BY L.[CreationDate] DESC
    ) PL
    WHERE LR.[RequesterId] = @RequesterId
    ORDER BY LR.[CreationDate] DESC

    SELECT
        AD.[AccessRequestId],
        AD.[DeciderKind] AS [DeciderKind],
        AD.[ApproverId] AS [Id],
        AU.[Name] AS [Name],
        AU.[Email] AS [Email],
        AD.[Comment] AS [Comment],
        AD.[Verdict] AS [Verdict],
        AD.[CreationDate] AS [DecidedAt]
    FROM [dbo].[AccessDecision] AD
    INNER JOIN [dbo].[AccessRequest] LR ON LR.[Id] = AD.[AccessRequestId]
    LEFT JOIN [dbo].[User] AU ON AU.[Id] = AD.[ApproverId]
    WHERE LR.[RequesterId] = @RequesterId
    ORDER BY AD.[AccessRequestId], AD.[CreationDate] ASC
END
GO

-- AccessRequest_ResolveWithDecision
CREATE OR ALTER PROCEDURE [dbo].[AccessRequest_ResolveWithDecision]
    @AccessRequestId UNIQUEIDENTIFIER,
    @Status TINYINT,
    @AccessDecisionId UNIQUEIDENTIFIER,
    @ApproverId UNIQUEIDENTIFIER,
    @Verdict TINYINT,
    @Comment NVARCHAR(MAX) = NULL,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- Atomically resolve a pending request and record the human approver's decision. The caller has already verified
    -- (and the application enforces) that the request is still Pending; the WHERE guard keeps the write idempotent
    -- under a race so a second approver can't move an already-resolved request.
    --
    -- Approval does not mint the lease: the requester activates the approved request later via
    -- [AccessLease_CreateFromApprovedRequest]. The automatic path ([AccessRequest_CreateAutoApproved]) records the
    -- approved request the same way and likewise leaves the lease to be minted at activation.
    BEGIN TRANSACTION AccessRequest_Resolve

    UPDATE [dbo].[AccessRequest]
    SET [Status] = @Status,
        [ResolvedDate] = @Now
    WHERE [Id] = @AccessRequestId AND [Status] = 0 -- Pending

    INSERT INTO [dbo].[AccessDecision]
    (
        [Id], [AccessRequestId], [DeciderKind], [ApproverId], [ConditionKind],
        [Verdict], [Comment], [EvaluationContext], [CreationDate]
    )
    VALUES
    (
        @AccessDecisionId, @AccessRequestId, 1 /* Human */, @ApproverId, NULL,
        @Verdict, @Comment, NULL, @Now
    )

    COMMIT TRANSACTION AccessRequest_Resolve
END
GO
