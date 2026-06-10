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
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    BEGIN TRANSACTION AccessLease_CreateAutoApproved

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
END
