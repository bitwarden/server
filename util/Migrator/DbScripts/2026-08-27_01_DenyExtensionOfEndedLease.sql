-- Records an extension of an ended lease as a Denied request, not a rollback.
-- The denied row counts toward the cap but is inert; the lease can't extend.
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
    @RuleId UNIQUEIDENTIFIER = NULL,
    @DenialComment NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON
    -- Holds the per-lease range lock until the writes commit, serializing concurrent extensions.
    SET XACT_ABORT ON

    BEGIN TRANSACTION

    -- Locks the parent lease so a concurrent extension blocks then re-counts.
    IF NOT EXISTS (
        SELECT 1
        FROM [dbo].[AccessLease] WITH (UPDLOCK, HOLDLOCK)
        WHERE [Id] = @ExtensionOfLeaseId
            AND [RequesterId] = @RequesterId
            AND [Status] = 0 /* Active */
            AND [NotAfter] > @Now
    )
    BEGIN
        -- Recorded as an answerable denied request; the window is stored but never applied.
        INSERT INTO [dbo].[AccessRequest]
        (
            [Id], [ExtensionOfLeaseId], [OrganizationId], [CollectionId], [CipherId], [RequesterId],
            [NotBefore], [NotAfter], [Reason], [Status], [CreationDate], [ResolvedDate], [RuleId]
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

    -- At most one extension per lease; counted under the lease lock, so it's race-safe.
    IF EXISTS (SELECT 1 FROM [dbo].[AccessRequest] WHERE [ExtensionOfLeaseId] = @ExtensionOfLeaseId)
    BEGIN
        ROLLBACK TRANSACTION
        SELECT -1 -- AlreadyExtended
        RETURN
    END

    -- Pushes the parent lease's end out; no new lease is minted.
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
