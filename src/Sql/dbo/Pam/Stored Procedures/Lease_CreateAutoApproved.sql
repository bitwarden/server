CREATE PROCEDURE [dbo].[Lease_CreateAutoApproved]
    @LeaseRequestId UNIQUEIDENTIFIER,
    @LeaseId UNIQUEIDENTIFIER,
    @LeaseDecisionId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @CollectionId UNIQUEIDENTIFIER,
    @CipherId UNIQUEIDENTIFIER,
    @RequesterId UNIQUEIDENTIFIER,
    @NotBefore DATETIME2(7),
    @NotAfter DATETIME2(7),
    @Reason NVARCHAR(MAX) = NULL,
    @PolicyKind NVARCHAR(50) = NULL,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    BEGIN TRANSACTION Lease_CreateAutoApproved

    -- The request is created already resolved (Approved). LeaseId stays NULL: it is reserved for extension
    -- requests; provenance for an original lease flows the other way, via Lease.LeaseRequestId.
    INSERT INTO [dbo].[LeaseRequest]
    (
        [Id], [LeaseId], [OrganizationId], [CollectionId], [CipherId], [RequesterId],
        [NotBefore], [NotAfter], [Reason], [Status], [CreationDate], [ResolvedDate]
    )
    VALUES
    (
        @LeaseRequestId, NULL, @OrganizationId, @CollectionId, @CipherId, @RequesterId,
        @NotBefore, @NotAfter, @Reason, 1 /* Approved */, @Now, @Now
    )

    INSERT INTO [dbo].[LeaseDecision]
    (
        [Id], [LeaseRequestId], [DeciderKind], [ApproverId], [PolicyKind],
        [Decision], [Comment], [EvaluationContext], [CreationDate]
    )
    VALUES
    (
        @LeaseDecisionId, @LeaseRequestId, 0 /* Policy */, NULL, @PolicyKind,
        0 /* Approve */, NULL, NULL, @Now
    )

    INSERT INTO [dbo].[Lease]
    (
        [Id], [LeaseRequestId], [OrganizationId], [CollectionId], [CipherId], [RequesterId],
        [Status], [NotBefore], [NotAfter], [RevokedDate], [RevokedBy], [CreationDate]
    )
    VALUES
    (
        @LeaseId, @LeaseRequestId, @OrganizationId, @CollectionId, @CipherId, @RequesterId,
        0 /* Active */, @NotBefore, @NotAfter, NULL, NULL, @Now
    )

    COMMIT TRANSACTION Lease_CreateAutoApproved
END
