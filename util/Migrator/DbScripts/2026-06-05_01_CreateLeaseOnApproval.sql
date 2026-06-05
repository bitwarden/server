-- PAM Credential Leasing: when an approver approves a human request, mint the active lease that authorizes access.
-- Previously [LeaseRequest_ResolveWithDecision] only flipped the request to Approved and recorded the decision, so the
-- human path never produced a [Lease] and the approved requester could not read the credential. This adds an optional
-- @LeaseId; when supplied (approvals only), the lease is created in the same transaction with the request's approved
-- window, mirroring [Lease_CreateAutoApproved] on the automatic path.

CREATE OR ALTER PROCEDURE [dbo].[LeaseRequest_ResolveWithDecision]
    @LeaseRequestId UNIQUEIDENTIFIER,
    @Status TINYINT,
    @LeaseDecisionId UNIQUEIDENTIFIER,
    @ApproverId UNIQUEIDENTIFIER,
    @Decision TINYINT,
    @Comment NVARCHAR(MAX) = NULL,
    @LeaseId UNIQUEIDENTIFIER = NULL,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- Atomically resolve a pending request and record the human approver's decision. The caller has already verified
    -- (and the application enforces) that the request is still Pending; the WHERE guard keeps the write idempotent
    -- under a race so a second approver can't move an already-resolved request.
    BEGIN TRANSACTION LeaseRequest_ResolveWithDecision

    UPDATE [dbo].[LeaseRequest]
    SET [Status] = @Status,
        [ResolvedDate] = @Now
    WHERE [Id] = @LeaseRequestId AND [Status] = 0 -- Pending

    INSERT INTO [dbo].[LeaseDecision]
    (
        [Id], [LeaseRequestId], [DeciderKind], [ApproverId], [PolicyKind],
        [Decision], [Comment], [EvaluationContext], [CreationDate]
    )
    VALUES
    (
        @LeaseDecisionId, @LeaseRequestId, 1 /* Human */, @ApproverId, NULL,
        @Decision, @Comment, NULL, @Now
    )

    -- An approval mints the active lease that authorizes access, mirroring [Lease_CreateAutoApproved] on the automatic
    -- path. @LeaseId is supplied only when approving; the lease window is the request's approved window, so the lease
    -- is found by [Lease_ReadActiveByRequesterIdCipherId] once @Now falls inside it.
    IF @LeaseId IS NOT NULL
    BEGIN
        INSERT INTO [dbo].[Lease]
        (
            [Id], [LeaseRequestId], [OrganizationId], [CollectionId], [CipherId], [RequesterId],
            [Status], [NotBefore], [NotAfter], [RevokedDate], [RevokedBy], [CreationDate]
        )
        SELECT
            @LeaseId, [Id], [OrganizationId], [CollectionId], [CipherId], [RequesterId],
            0 /* Active */, [NotBefore], [NotAfter], NULL, NULL, @Now
        FROM [dbo].[LeaseRequest]
        WHERE [Id] = @LeaseRequestId
    END

    COMMIT TRANSACTION LeaseRequest_ResolveWithDecision
END
GO
