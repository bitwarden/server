-- Makes sure a member can't end up with two open requests, or two live leases, for the same item when they submit or
-- activate twice at nearly the same time.

CREATE OR ALTER PROCEDURE [dbo].[AccessRequest_CreateIfNoOpen]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @CollectionId UNIQUEIDENTIFIER,
    @CipherId UNIQUEIDENTIFIER,
    @RequesterId UNIQUEIDENTIFIER,
    @NotBefore DATETIME2(7),
    @NotAfter DATETIME2(7),
    @Reason NVARCHAR(MAX) = NULL,
    @CreationDate DATETIME2(7),
    @RuleId UNIQUEIDENTIFIER = NULL,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON
    SET XACT_ABORT ON

    BEGIN TRANSACTION

    -- Serializes submits and activations per requester and item; the lock is released at commit or rollback.
    DECLARE @Resource NVARCHAR(255) = CONCAT(N'PamAccess:', @RequesterId, N':', @CipherId)
    DECLARE @Lock INT
    EXEC @Lock = sp_getapplock @Resource = @Resource, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000
    IF @Lock < 0
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 50000, 'Could not acquire the access submission lock.', 1
    END

    -- Same predicates as AccessLease_ReadActiveByRequesterIdCipherId.
    IF EXISTS (
        SELECT 1 FROM [dbo].[AccessLease]
        WHERE [RequesterId] = @RequesterId AND [CipherId] = @CipherId
            AND [Action] = 0 AND [NotBefore] <= @Now AND [NotAfter] > @Now
    )
    BEGIN
        ROLLBACK TRANSACTION
        SELECT -1 -- ActiveLeaseExists
        RETURN
    END

    -- Same predicates as the pending read.
    IF EXISTS (
        SELECT 1 FROM [dbo].[AccessRequest] AR
        WHERE AR.[RequesterId] = @RequesterId AND AR.[CipherId] = @CipherId AND AR.[NotAfter] > @Now
            AND AR.[Action] = 0
    )
    BEGIN
        ROLLBACK TRANSACTION
        SELECT 0 -- PendingRequestExists
        RETURN
    END

    -- Same predicates as the approved-unactivated read.
    IF EXISTS (
        SELECT 1 FROM [dbo].[AccessRequest] AR
        WHERE AR.[RequesterId] = @RequesterId AND AR.[CipherId] = @CipherId AND AR.[NotAfter] > @Now
            AND AR.[Action] = 1 AND AR.[ExtensionOfLeaseId] IS NULL
            AND NOT EXISTS (SELECT 1 FROM [dbo].[AccessLease] AL WHERE AL.[AccessRequestId] = AR.[Id])
    )
    BEGIN
        ROLLBACK TRANSACTION
        SELECT -2 -- ApprovedRequestExists
        RETURN
    END

    INSERT INTO [dbo].[AccessRequest]
    (
        [Id], [ExtensionOfLeaseId], [OrganizationId], [CollectionId], [CipherId], [RequesterId],
        [NotBefore], [NotAfter], [Reason], [Action], [CreationDate], [ActionDate], [RuleId]
    )
    VALUES
    (
        @Id, NULL, @OrganizationId, @CollectionId, @CipherId, @RequesterId,
        @NotBefore, @NotAfter, @Reason, 0 /* None (open) */, @CreationDate, NULL, @RuleId
    )

    COMMIT TRANSACTION

    SELECT 1 -- Created
END

GO

CREATE OR ALTER PROCEDURE [dbo].[AccessRequest_CreateAutoApproved]
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

    -- Same predicates as the pending read.
    IF EXISTS (
        SELECT 1 FROM [dbo].[AccessRequest] AR
        WHERE AR.[RequesterId] = @RequesterId AND AR.[CipherId] = @CipherId AND AR.[NotAfter] > @CreationDate
            AND AR.[Action] = 0
    )
    BEGIN
        ROLLBACK TRANSACTION
        SELECT 0 -- PendingRequestExists
        RETURN
    END

    -- Same predicates as the approved-unactivated read.
    IF EXISTS (
        SELECT 1 FROM [dbo].[AccessRequest] AR
        WHERE AR.[RequesterId] = @RequesterId AND AR.[CipherId] = @CipherId AND AR.[NotAfter] > @CreationDate
            AND AR.[Action] = 1 AND AR.[ExtensionOfLeaseId] IS NULL
            AND NOT EXISTS (SELECT 1 FROM [dbo].[AccessLease] AL WHERE AL.[AccessRequestId] = AR.[Id])
    )
    BEGIN
        ROLLBACK TRANSACTION
        SELECT -2 -- ApprovedRequestExists
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

GO

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

    -- Taken before the claim, the same order request submission uses, so the two never wait on each other.
    DECLARE @CipherId UNIQUEIDENTIFIER = (SELECT [CipherId] FROM [dbo].[AccessRequest] WHERE [Id] = @AccessRequestId)
    DECLARE @Resource NVARCHAR(255) = CONCAT(N'PamAccess:', @RequesterId, N':', @CipherId)
    DECLARE @Lock INT
    EXEC @Lock = sp_getapplock @Resource = @Resource, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 10000
    IF @Lock < 0
    BEGIN
        ROLLBACK TRANSACTION;
        THROW 50000, 'Could not acquire the access submission lock.', 1
    END

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

    -- At most one live lease per requester and item, whatever the rule.
    IF EXISTS (
        SELECT 1 FROM [dbo].[AccessLease]
        WHERE [RequesterId] = @RequesterId AND [CipherId] = @CipherId
            AND [Action] = 0 AND [NotBefore] <= @Now AND [NotAfter] > @Now
    )
    BEGIN
        ROLLBACK TRANSACTION
        SELECT -2
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

    -- [NotBefore] is @Now, never backdated; preconditions restated as defense in depth.
    INSERT INTO [dbo].[AccessLease]
    (
        [Id], [AccessRequestId], [OrganizationId], [CollectionId], [CipherId], [RequesterId],
        [Action], [NotBefore], [NotAfter], [RevokedDate], [RevokedBy], [CreationDate]
    )
    SELECT
        @AccessLeaseId, AR.[Id], AR.[OrganizationId], AR.[CollectionId], AR.[CipherId], AR.[RequesterId],
        0 /* None (no early end) */, @Now, AR.[NotAfter], NULL, NULL, @Now
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

    -- 1 = minted, 0 = precondition no longer held (caller re-reads the winner), -1 = cipher singleton conflict,
    -- -2 = requester already holds a live lease on the item.
    SELECT CASE WHEN @Rows = 1 THEN 1 ELSE 0 END
END

GO

-- Covers the in-window lease checks so they never fetch the row. A revoke locks the row first and the index second,
-- so an activation that read the index and then fetched the row could deadlock with it.
IF EXISTS (SELECT 1 FROM sys.indexes WHERE [name] = 'IX_AccessLease_CipherId_Action_NotAfter' AND [object_id] = OBJECT_ID('[dbo].[AccessLease]'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_AccessLease_CipherId_Action_NotAfter]
        ON [dbo].[AccessLease] ([CipherId] ASC, [Action] ASC, [NotAfter] DESC)
        INCLUDE ([NotBefore], [RequesterId])
        WITH (DROP_EXISTING = ON);
END
GO
