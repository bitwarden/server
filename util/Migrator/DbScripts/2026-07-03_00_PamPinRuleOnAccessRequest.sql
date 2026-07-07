-- Pin the resolved governing rule on the access request: add AccessRequest.RuleId (nullable FK to AccessRule) and
-- thread it through the create + detail-read stored procedures. The rule is resolved once at submit (oldest wins) and
-- stored here, so downstream reads can use the pinned rule instead of re-resolving. Nullable + no backfill: rows created
-- before this migration keep RuleId NULL.

-- Add the column (idempotent).
IF COL_LENGTH('[dbo].[AccessRequest]', 'RuleId') IS NULL
BEGIN
    ALTER TABLE [dbo].[AccessRequest]
        ADD [RuleId] UNIQUEIDENTIFIER NULL;
END
GO

-- Add the FK to AccessRule (idempotent). NULL RuleId values are not FK-checked, so pre-existing rows are unaffected.
IF OBJECT_ID('[dbo].[FK_AccessRequest_AccessRule]', 'F') IS NULL
BEGIN
    ALTER TABLE [dbo].[AccessRequest]
        ADD CONSTRAINT [FK_AccessRequest_AccessRule] FOREIGN KEY ([RuleId]) REFERENCES [dbo].[AccessRule] ([Id]);
END
GO

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
    @RejectedDate DATETIME2(7) = NULL,
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
        [RejectedDate],
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
        @RejectedDate,
        @RuleId
    )
END
GO

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
