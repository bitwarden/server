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
    @Now DATETIME2(7),
    @RuleId UNIQUEIDENTIFIER = NULL,
    @DenialComment NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON
    -- Holds the per-lease range lock until writes commit, serializing concurrent extensions.
    SET XACT_ABORT ON

    BEGIN TRANSACTION

    -- Locks the parent lease; must have no early end and be in-window to extend.
    IF NOT EXISTS (
        SELECT 1
        FROM [dbo].[AccessLease] WITH (UPDLOCK, HOLDLOCK)
        WHERE [Id] = @ExtensionOfLeaseId
            AND [RequesterId] = @RequesterId
            AND [Action] = 0 /* None (no early end) */
            AND [NotAfter] > @Now
    )
    BEGIN
        -- Records a denied, answerable request that still counts toward the extension cap.
        INSERT INTO [dbo].[AccessRequest]
        (
            [Id], [ExtensionOfLeaseId], [OrganizationId], [CollectionId], [CipherId], [RequesterId],
            [NotBefore], [NotAfter], [Reason], [Action], [CreationDate], [ActionDate], [RuleId]
        )
        VALUES
        (
            @AccessRequestId, @ExtensionOfLeaseId, @OrganizationId, @CollectionId, @CipherId, @RequesterId,
            @NotBefore, @NotAfter, @Reason, 2 /* Denied */, @Now, @Now, @RuleId
        )

        INSERT INTO [dbo].[AccessDecision]
        (
            [Id], [AccessRequestId], [DeciderKind], [ApproverId], [ConditionKind],
            [Verdict], [Comment], [EvaluationContext], [CreationDate]
        )
        VALUES
        (
            @AccessDecisionId, @AccessRequestId, 0 /* Automatic */, NULL, NULL,
            0 /* Deny */, @DenialComment, NULL, @Now
        )

        COMMIT TRANSACTION
        SELECT 0 -- LeaseNotActive
        RETURN
    END

    -- Extension is capped at one per lease; counted under the same lock, race-safe.
    IF EXISTS (SELECT 1 FROM [dbo].[AccessRequest] WHERE [ExtensionOfLeaseId] = @ExtensionOfLeaseId)
    BEGIN
        ROLLBACK TRANSACTION
        SELECT -1 -- AlreadyExtended
        RETURN
    END

    -- No new lease is minted; extending pushes the parent lease's NotAfter out in place.
    INSERT INTO [dbo].[AccessRequest]
    (
        [Id], [ExtensionOfLeaseId], [OrganizationId], [CollectionId], [CipherId], [RequesterId],
        [NotBefore], [NotAfter], [Reason], [Action], [CreationDate], [ActionDate], [RuleId]
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
