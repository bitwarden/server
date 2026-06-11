-- PAM Credential Leasing: the automatic (no-approval) path no longer mints a lease at submit. Like the human path,
-- it now only records the request — already Approved, with its automatic verdict — and the requester activates the
-- approved request when they actually want access ([AccessLease_CreateFromApprovedRequest]), which is when the active
-- lease is minted. This makes "start the lease" an explicit caller action on both paths and leaves the per-cipher
-- single-active-lease guard living on the single remaining mint site (activation).
--
-- [AccessRequest_CreateAutoApproved] replaces [AccessLease_CreateAutoApproved]: it writes the request + automatic
-- decision in one transaction and inserts no lease. The old proc is dropped outright rather than left as a no-longer
-- called shim. Acceptable here — the feature is an unshipped POC behind the pm-37044-pam-v-0 flag and server +
-- migration deploy together.

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
    @ConditionKind NVARCHAR(50) = NULL,
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
        0 /* Approve */, NULL, NULL, @CreationDate
    )

    COMMIT TRANSACTION AccessRequest_CreateAutoApproved
END
GO

DROP PROCEDURE IF EXISTS [dbo].[AccessLease_CreateAutoApproved]
GO
