-- The retraction and resolve procedures report whether their guarded write landed, so a caller that lost a race
-- can refuse instead of describing a write that never happened.

CREATE OR ALTER PROCEDURE [dbo].[AccessRequest_ResolveWithDecision]
    @AccessRequestId UNIQUEIDENTIFIER,
    @Action TINYINT,
    @AccessDecisionId UNIQUEIDENTIFIER,
    @ApproverId UNIQUEIDENTIFIER,
    @Verdict TINYINT,
    @Comment NVARCHAR(MAX) = NULL,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON
    -- XACT_ABORT rolls back both writes together on any failure.
    SET XACT_ABORT ON

    -- Records an approver's decision; WHERE guard makes it idempotent (first CAS wins).
    BEGIN TRANSACTION AccessRequest_Resolve

    UPDATE [dbo].[AccessRequest]
    SET [Action] = @Action,
        [ActionDate] = @Now
    WHERE [Id] = @AccessRequestId
        AND [Action] = 0 -- None (open)
        AND [NotAfter] > @Now

    DECLARE @Rows INT = @@ROWCOUNT

    IF @Rows > 0
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

    COMMIT TRANSACTION AccessRequest_Resolve

    -- 1 when this call resolved the request, 0 when it was no longer open.
    SELECT CAST(CASE WHEN @Rows > 0 THEN 1 ELSE 0 END AS BIT)
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessRequest_Cancel]
    @AccessRequestId UNIQUEIDENTIFIER,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON
    -- Explicit transaction needed; autocommit would release the claim's lock before the check.
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

    DECLARE @Rows INT = @@ROWCOUNT

    COMMIT TRANSACTION AccessRequest_Cancel

    -- 1 when this call withdrew the request, 0 when it was no longer withdrawable.
    SELECT CAST(CASE WHEN @Rows > 0 THEN 1 ELSE 0 END AS BIT)
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

    -- Claims the row first, like [AccessRequest_Cancel], to serialize against a concurrent activation.
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

    DECLARE @Rows INT = @@ROWCOUNT

    IF @Rows > 0
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

    -- 1 when this call retracted the request, 0 when it was no longer retractable.
    SELECT CAST(CASE WHEN @Rows > 0 THEN 1 ELSE 0 END AS BIT)
END
GO
