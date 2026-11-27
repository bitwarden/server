CREATE PROCEDURE [dbo].[AccessRequest_CreateIfNoOpen]
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
