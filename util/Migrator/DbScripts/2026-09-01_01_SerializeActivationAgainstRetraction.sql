-- Write skew fix: activation and retraction now both claim [AccessRequest] before writing [AccessLease].
-- Claim order must match on both sides; reversed, it deadlocks (1205).
CREATE OR ALTER PROCEDURE [dbo].[AccessLease_CreateFromApprovedRequest]
    @AccessLeaseId UNIQUEIDENTIFIER,
    @AccessRequestId UNIQUEIDENTIFIER,
    @RequesterId UNIQUEIDENTIFIER,
    @Now DATETIME2(7),
    @EnforceSingleActiveLease BIT = 0
AS
BEGIN
    SET NOCOUNT ON
    -- Holds the claim's row lock and the singleton guard's range lock until INSERT commits.
    SET XACT_ABORT ON

    BEGIN TRANSACTION

    -- Claims the request row first so activation/retraction serialize on it; reversed order deadlocks (1205).
    UPDATE [dbo].[AccessRequest]
    SET [Action] = 1 -- Approved: unchanged, the write is what matters
    WHERE
        [Id] = @AccessRequestId
        AND [RequesterId] = @RequesterId
        AND [Action] = 1 -- Approved
        AND [ExtensionOfLeaseId] IS NULL -- an extension applied in place on approval and never mints a lease
        AND [NotBefore] <= @Now
        AND [NotAfter] > @Now
        AND NOT EXISTS (SELECT 1 FROM [dbo].[AccessLease] AL WHERE AL.[AccessRequestId] = @AccessRequestId)

    IF @@ROWCOUNT = 0
    BEGIN
        ROLLBACK TRANSACTION
        SELECT 0
        RETURN
    END

    -- At most one active lease per cipher; the UPDLOCK, HOLDLOCK lock serializes concurrent activation.
    IF @EnforceSingleActiveLease = 1
        AND EXISTS (
            SELECT 1
            FROM [dbo].[AccessLease] WITH (UPDLOCK, HOLDLOCK)
            WHERE [CipherId] = (SELECT [CipherId] FROM [dbo].[AccessRequest] WHERE [Id] = @AccessRequestId)
                AND [Action] = 0 /* None (no early end) */
                AND [NotBefore] <= @Now
                AND [NotAfter] > @Now
        )
    BEGIN
        ROLLBACK TRANSACTION
        SELECT -1
        RETURN
    END

    -- [NotBefore] is @Now, not AR.[NotBefore]: a lease is never backdated.
    -- Preconditions are restated as defense in depth; the claim above already holds the row.
    INSERT INTO [dbo].[AccessLease]
    (
        [Id], [AccessRequestId], [OrganizationId], [CollectionId], [CipherId], [RequesterId],
        [Action], [NotBefore], [NotAfter], [RevokedDate], [RevokedBy], [CreationDate]
    )
    SELECT
        @AccessLeaseId, AR.[Id], AR.[OrganizationId], AR.[CollectionId], AR.[CipherId], AR.[RequesterId],
        0 /* None (no early end) */, AR.[NotBefore], AR.[NotAfter], NULL, NULL, @Now
    FROM [dbo].[AccessRequest] AR
    WHERE
        AR.[Id] = @AccessRequestId
        AND AR.[RequesterId] = @RequesterId
        AND AR.[Action] = 1 -- Approved
        AND AR.[ExtensionOfLeaseId] IS NULL -- an extension applied in place on approval and never mints a lease
        AND AR.[NotBefore] <= @Now
        AND AR.[NotAfter] > @Now
        AND NOT EXISTS (SELECT 1 FROM [dbo].[AccessLease] AL WHERE AL.[AccessRequestId] = AR.[Id])

    DECLARE @Rows INT = @@ROWCOUNT

    COMMIT TRANSACTION

    -- 1 = minted, 0 = precondition no longer held (caller re-reads the winner).
    SELECT CASE WHEN @Rows = 1 THEN 1 ELSE 0 END
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessRequest_Cancel]
    @AccessRequestId UNIQUEIDENTIFIER,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON
    -- Requires an explicit transaction; autocommit would release the claim's lock right after the SELECT.
    SET XACT_ABORT ON

    BEGIN TRANSACTION AccessRequest_Cancel

    -- Claims the row before the lease probe so a concurrent activation can't mint meanwhile.
    DECLARE @Claimed TINYINT
    SELECT @Claimed = [Action]
    FROM [dbo].[AccessRequest] WITH (UPDLOCK, ROWLOCK)
    WHERE [Id] = @AccessRequestId

    -- Requester withdrawal of a not-yet-activated request; no AccessDecision, since it isn't an approver verdict.
    UPDATE [dbo].[AccessRequest]
    SET [Action] = 3, -- Cancelled
        [ActionDate] = @Now
    WHERE [Id] = @AccessRequestId
        AND [Action] IN (0, 1) -- None (open) or Approved
        AND [NotAfter] > @Now
        AND NOT EXISTS (SELECT 1 FROM [dbo].[AccessLease] L WHERE L.[AccessRequestId] = @AccessRequestId)

    COMMIT TRANSACTION AccessRequest_Cancel
END
GO

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
    -- Both writes commit or roll back together (XACT_ABORT).
    SET XACT_ABORT ON

    -- Approver retraction of a not-yet-activated request; AccessDecision is inserted only on an actual transition.
    BEGIN TRANSACTION AccessRequest_CancelWithDecision

    -- Claims the row first, like [AccessRequest_Cancel], to serialize against a concurrent activation's claim.
    DECLARE @Claimed TINYINT
    SELECT @Claimed = [Action]
    FROM [dbo].[AccessRequest] WITH (UPDLOCK, ROWLOCK)
    WHERE [Id] = @AccessRequestId

    UPDATE [dbo].[AccessRequest]
    SET [Action] = 2, -- Denied
        [ActionDate] = @Now
    WHERE [Id] = @AccessRequestId
        AND [Action] IN (0, 1) -- None (open) or Approved
        AND [NotAfter] > @Now
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
