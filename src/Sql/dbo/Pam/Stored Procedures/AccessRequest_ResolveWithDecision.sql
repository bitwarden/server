CREATE PROCEDURE [dbo].[AccessRequest_ResolveWithDecision]
    @AccessRequestId UNIQUEIDENTIFIER,
    @Status TINYINT,
    @AccessDecisionId UNIQUEIDENTIFIER,
    @ApproverId UNIQUEIDENTIFIER,
    @Verdict TINYINT,
    @Comment NVARCHAR(MAX) = NULL,
    @AccessLeaseId UNIQUEIDENTIFIER = NULL,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- Atomically resolve a pending request and record the human approver's decision. The caller has already verified
    -- (and the application enforces) that the request is still Pending; the WHERE guard keeps the write idempotent
    -- under a race so a second approver can't move an already-resolved request.
    BEGIN TRANSACTION AccessRequest_Resolve

    UPDATE [dbo].[AccessRequest]
    SET [Status] = @Status,
        [ResolvedDate] = @Now
    WHERE [Id] = @AccessRequestId AND [Status] = 0 -- Pending

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

    -- An approval mints the active lease that authorizes access, mirroring [AccessLease_CreateAutoApproved] on the automatic
    -- path. @AccessLeaseId is supplied only when approving; the lease window is the request's approved window, so the lease
    -- is found by [AccessLease_ReadActiveByRequesterIdCipherId] once @Now falls inside it.
    IF @AccessLeaseId IS NOT NULL
    BEGIN
        INSERT INTO [dbo].[AccessLease]
        (
            [Id], [AccessRequestId], [OrganizationId], [CollectionId], [CipherId], [RequesterId],
            [Status], [NotBefore], [NotAfter], [RevokedDate], [RevokedBy], [CreationDate]
        )
        SELECT
            @AccessLeaseId, [Id], [OrganizationId], [CollectionId], [CipherId], [RequesterId],
            0 /* Active */, [NotBefore], [NotAfter], NULL, NULL, @Now
        FROM [dbo].[AccessRequest]
        WHERE [Id] = @AccessRequestId
    END

    COMMIT TRANSACTION AccessRequest_Resolve
END
