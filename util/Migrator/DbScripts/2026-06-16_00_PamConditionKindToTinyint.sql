-- PAM Credential Leasing: convert [AccessDecision].[ConditionKind] from a magic-string NVARCHAR(50) to a TINYINT,
-- backed by the new AccessConditionKind enum (HumanApproval = 0, IpAllowlist = 1, TimeOfDay = 2). The column records
-- which condition produced an automatic decision; it is internal-only (never returned to clients) and currently always
-- NULL, so no data backfill is required. The conditions JSON keeps its string `kind` discriminator. Acceptable as a
-- straight type change here: the feature is an unshipped POC behind the pm-37044-pam-v-0 flag and the server +
-- migration deploy together.

IF COL_LENGTH('[dbo].[AccessDecision]', 'ConditionKind') IS NOT NULL
BEGIN
    ALTER TABLE [dbo].[AccessDecision]
        ALTER COLUMN [ConditionKind] TINYINT NULL;
END
GO

-- Re-create the only proc that takes @ConditionKind as a parameter so its type matches the column.
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
        0 /* Approve */, NULL, NULL, @CreationDate
    )

    COMMIT TRANSACTION AccessRequest_CreateAutoApproved
END
GO
