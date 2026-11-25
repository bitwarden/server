CREATE PROCEDURE [dbo].[AccessRequest_CreateAutoApproved]
    @AccessRequestId UNIQUEIDENTIFIER,
    @AccessDecisionId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @CollectionId UNIQUEIDENTIFIER,
    @CipherId UNIQUEIDENTIFIER,
    @RequesterId UNIQUEIDENTIFIER,
    @NotBefore DATETIME2(7),
    @NotAfter DATETIME2(7),
    @Reason NVARCHAR(MAX) = NULL,
    @ConditionKind TINYINT = NULL,
    @CreationDate DATETIME2(7),
    @RuleId UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON
    -- XACT_ABORT rolls both writes back together on any failure.
    SET XACT_ABORT ON

    -- Records an auto-approved request and verdict; no lease minted here, activation happens later.
    BEGIN TRANSACTION AccessRequest_CreateAutoApproved

    -- Serializes submits and activations per requester and item; the lock is released at commit or rollback.
    DECLARE @Resource NVARCHAR(255) = CONCAT(N'PamAccess:', @RequesterId, N':', @CipherId)
    DECLARE @Lock INT
    EXEC @Lock = sp_getapplock @Resource = @Resource, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000
    IF @Lock < 0
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 50000, 'Could not acquire the access submission lock.', 1
    END

    -- @CreationDate is the submit time. Same predicates as AccessLease_ReadActiveByRequesterIdCipherId.
    IF EXISTS (
        SELECT 1 FROM [dbo].[AccessLease]
        WHERE [RequesterId] = @RequesterId AND [CipherId] = @CipherId
            AND [Action] = 0 AND [NotBefore] <= @CreationDate AND [NotAfter] > @CreationDate
    )
    BEGIN
        ROLLBACK TRANSACTION
        SELECT -1 -- ActiveLeaseExists
        RETURN
    END

    -- Same predicates as the pending and approved-unactivated reads.
    IF EXISTS (
        SELECT 1 FROM [dbo].[AccessRequest] AR
        WHERE AR.[RequesterId] = @RequesterId AND AR.[CipherId] = @CipherId AND AR.[NotAfter] > @CreationDate
            AND (
                AR.[Action] = 0
                OR (
                    AR.[Action] = 1 AND AR.[ExtensionOfLeaseId] IS NULL
                    AND NOT EXISTS (SELECT 1 FROM [dbo].[AccessLease] AL WHERE AL.[AccessRequestId] = AR.[Id])
                )
            )
    )
    BEGIN
        ROLLBACK TRANSACTION
        SELECT 0 -- OpenRequestExists
        RETURN
    END

    -- ExtensionOfLeaseId stays NULL; provenance instead flows via AccessLease.AccessRequestId.
    INSERT INTO [dbo].[AccessRequest]
    (
        [Id], [ExtensionOfLeaseId], [OrganizationId], [CollectionId], [CipherId], [RequesterId],
        [NotBefore], [NotAfter], [Reason], [Action], [CreationDate], [ActionDate], [RuleId]
    )
    VALUES
    (
        @AccessRequestId, NULL, @OrganizationId, @CollectionId, @CipherId, @RequesterId,
        @NotBefore, @NotAfter, @Reason, 1 /* Approved */, @CreationDate, @CreationDate, @RuleId
    )

    INSERT INTO [dbo].[AccessDecision]
    (
        [Id], [AccessRequestId], [DeciderKind], [ApproverId], [ConditionKind],
        [Verdict], [Comment], [EvaluationContext], [CreationDate]
    )
    VALUES
    (
        @AccessDecisionId, @AccessRequestId, 0 /* Automatic */, NULL, @ConditionKind,
        1 /* Approve */, NULL, NULL, @CreationDate
    )

    COMMIT TRANSACTION AccessRequest_CreateAutoApproved

    SELECT 1 -- Created
END
