-- Adds NotAfter DESC to the per-cipher lease index so AccessLease_ReadActiveByCipherId seeks in-window rows
-- without a sort, under a name that says so. Created before its predecessors are dropped, so the singleton
-- guard is never left without an index.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_AccessLease_CipherId_Action_NotAfter' AND [object_id] = OBJECT_ID('[dbo].[AccessLease]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AccessLease_CipherId_Action_NotAfter]
        ON [dbo].[AccessLease] ([CipherId] ASC, [Action] ASC, [NotAfter] DESC);
END
GO

IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_AccessLease_CipherId_Action' AND [object_id] = OBJECT_ID('[dbo].[AccessLease]'))
BEGIN
    DROP INDEX [IX_AccessLease_CipherId_Action] ON [dbo].[AccessLease];
END
GO

IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_AccessLease_CipherId_Status' AND [object_id] = OBJECT_ID('[dbo].[AccessLease]'))
BEGIN
    DROP INDEX [IX_AccessLease_CipherId_Status] ON [dbo].[AccessLease];
END
GO

-- A lapsed window reads Expired, so neither a late verdict nor a late revoke may restamp it.
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

    COMMIT TRANSACTION AccessRequest_Resolve
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessLease_Revoke]
    @AccessLeaseId UNIQUEIDENTIFIER,
    @Action TINYINT,
    @RevokedBy UNIQUEIDENTIFIER,
    @AccessDecisionId UNIQUEIDENTIFIER,
    @Reason NVARCHAR(MAX) = NULL,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON
    -- XACT_ABORT rolls back both writes together on any failure.
    SET XACT_ABORT ON

    -- Ends a running lease (2 Revoked or 3 Cancelled), idempotently; feeds a Deny AccessDecision.
    DECLARE @Ended TABLE ([AccessRequestId] UNIQUEIDENTIFIER)

    BEGIN TRANSACTION AccessLease_Revoke

    UPDATE [dbo].[AccessLease]
    SET [Action] = @Action,
        [RevokedDate] = @Now,
        [RevokedBy] = @RevokedBy
    OUTPUT INSERTED.[AccessRequestId] INTO @Ended
    WHERE [Id] = @AccessLeaseId
        AND [Action] = 0 -- None (no early end)
        AND [NotAfter] > @Now

    INSERT INTO [dbo].[AccessDecision]
    (
        [Id], [AccessRequestId], [DeciderKind], [ApproverId], [ConditionKind],
        [Verdict], [Comment], [EvaluationContext], [CreationDate]
    )
    SELECT
        @AccessDecisionId, E.[AccessRequestId], 1 /* Human */, @RevokedBy, NULL,
        0 /* Deny */, @Reason, NULL, @Now
    FROM @Ended E

    COMMIT TRANSACTION AccessLease_Revoke
END
GO
