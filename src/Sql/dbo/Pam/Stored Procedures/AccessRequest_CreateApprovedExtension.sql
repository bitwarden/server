CREATE PROCEDURE [dbo].[AccessRequest_CreateApprovedExtension]
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
