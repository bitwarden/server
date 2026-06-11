CREATE PROCEDURE [dbo].[AccessLease_CreateAutoApproved]
    @AccessRequestId UNIQUEIDENTIFIER,
    @AccessLeaseId UNIQUEIDENTIFIER,
    @AccessDecisionId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @CollectionId UNIQUEIDENTIFIER,
    @CipherId UNIQUEIDENTIFIER,
    @RequesterId UNIQUEIDENTIFIER,
    @NotBefore DATETIME2(7),
    @NotAfter DATETIME2(7),
    @Reason NVARCHAR(MAX) = NULL,
    @ConditionKind NVARCHAR(50) = NULL,
    @Now DATETIME2(7),
    @EnforceSingleActiveLease BIT = 0
AS
BEGIN
    SET NOCOUNT ON

    BEGIN TRANSACTION AccessLease_CreateAutoApproved

    -- Per-cipher singleton guard. When the governing rule(s) ask for a single active lease, the auto-approved lease
    -- is minted only if no other active in-window lease exists for the same cipher across all users. The check runs
    -- inside the transaction before any insert so the UPDLOCK, HOLDLOCK range lock serializes concurrent activations
    -- of the same cipher; a conflict rolls back leaving nothing persisted and returns -1.
    IF @EnforceSingleActiveLease = 1
    BEGIN
        IF EXISTS (
            SELECT 1
            FROM [dbo].[AccessLease] WITH (UPDLOCK, HOLDLOCK)
            WHERE [CipherId] = @CipherId
                AND [Status] = 0 /* Active */
                AND [NotBefore] <= @Now
                AND [NotAfter] > @Now
        )
        BEGIN
            ROLLBACK TRANSACTION AccessLease_CreateAutoApproved
            SELECT -1
            RETURN
        END
    END

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
        @NotBefore, @NotAfter, @Reason, 1 /* Approved */, @Now, @Now
    )

    INSERT INTO [dbo].[AccessDecision]
    (
        [Id], [AccessRequestId], [DeciderKind], [ApproverId], [ConditionKind],
        [Verdict], [Comment], [EvaluationContext], [CreationDate]
    )
    VALUES
    (
        @AccessDecisionId, @AccessRequestId, 0 /* Automatic */, NULL, @ConditionKind,
        0 /* Approve */, NULL, NULL, @Now
    )

    INSERT INTO [dbo].[AccessLease]
    (
        [Id], [AccessRequestId], [OrganizationId], [CollectionId], [CipherId], [RequesterId],
        [Status], [NotBefore], [NotAfter], [RevokedDate], [RevokedBy], [CreationDate]
    )
    VALUES
    (
        @AccessLeaseId, @AccessRequestId, @OrganizationId, @CollectionId, @CipherId, @RequesterId,
        0 /* Active */, @NotBefore, @NotAfter, NULL, NULL, @Now
    )

    COMMIT TRANSACTION AccessLease_CreateAutoApproved

    -- 1 = minted (request + decision + lease all written).
    SELECT 1
END
