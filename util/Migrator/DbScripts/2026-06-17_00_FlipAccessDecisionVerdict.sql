-- Flip the AccessDecisionVerdict encoding so the values read naturally falsy/truthy: Deny = 0, Approve = 1
-- (previously Approve = 0, Deny = 1). The member names are unchanged; only the numeric wire/stored values swap.
--
-- DATA: any existing AccessDecision rows were written under the old encoding, so this migration MUST flip their
-- stored Verdict in the same step the procedures change -- otherwise every historical verdict inverts. The CASE
-- swap below is a no-op on an empty table and self-inverse, but this migration is NOT safe to run twice.
UPDATE [dbo].[AccessDecision]
SET [Verdict] = CASE [Verdict] WHEN 0 THEN 1 ELSE 0 END
GO

-- Auto-approved request: the automatic verdict literal moves from 0 to 1 (Approve).
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
    @CreationDate DATETIME2(7)
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
        [NotBefore], [NotAfter], [Reason], [Status], [CreationDate], [ResolvedDate]
    )
    VALUES
    (
        @AccessRequestId, NULL, @OrganizationId, @CollectionId, @CipherId, @RequesterId,
        @NotBefore, @NotAfter, @Reason, 1 /* Approved */, @CreationDate, @CreationDate
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

-- Auto-approved extension: the automatic verdict literal moves from 0 to 1 (Approve).
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
    @Now DATETIME2(7)
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
        [NotBefore], [NotAfter], [Reason], [Status], [CreationDate], [ResolvedDate]
    )
    VALUES
    (
        @AccessRequestId, @ExtensionOfLeaseId, @OrganizationId, @CollectionId, @CipherId, @RequesterId,
        @NotBefore, @NotAfter, @Reason, 1 /* Approved */, @Now, @Now
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

-- Lease revocation: the human Deny verdict literal moves from 1 to 0 (Deny).
CREATE OR ALTER PROCEDURE [dbo].[AccessLease_Revoke]
    @AccessLeaseId UNIQUEIDENTIFIER,
    @AccessRequestId UNIQUEIDENTIFIER,
    @RevokedBy UNIQUEIDENTIFIER,
    @AccessDecisionId UNIQUEIDENTIFIER,
    @Reason NVARCHAR(MAX) = NULL,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- Atomically revoke an active lease and capture who/why. The revocation reason has no dedicated column, so it is
    -- preserved as a human AccessDecision (Deny) against the lease's originating request, keeping the audit trail
    -- without a schema change. The WHERE guard keeps revocation idempotent if two approvers race.
    BEGIN TRANSACTION AccessLease_Revoke

    UPDATE [dbo].[AccessLease]
    SET [Status] = 2 /* Revoked */,
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
