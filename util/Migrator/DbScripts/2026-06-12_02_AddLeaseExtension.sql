-- PAM Credential Leasing: lease extension.
--
-- A member holding an active lease on an extension-enabled item (see AccessRule.AllowsExtensions / MaxExtensions)
-- may extend it. Extensions are always auto-approved: an already-Approved AccessRequest with a non-NULL
-- ExtensionOfLeaseId is recorded together with its automatic AccessDecision, and the parent lease's NotAfter is
-- pushed out in place — no new lease is minted, so the single-active-lease invariant is preserved.
--
--   * AccessRequest_CreateApprovedExtension   - atomic, per-lease-locked: guards the lease is active and the
--                                               per-rule max has not been reached, then writes the request +
--                                               decision and extends the lease.
--   * AccessRequest_CountExtensionsByLeaseId  - extension count for a lease (cap pre-check + UI "remaining").
--   * AccessRequest_ReadActiveApprovedByRequesterIdCipherId - now excludes extension requests, which extend the
--                                               parent lease in place and must never surface as activatable.
--
-- PAM is an unshipped POC behind the pm-37044-pam-v-0 flag with no production data; server + migration deploy
-- together, so the affected proc is altered in place rather than versioned.

CREATE OR ALTER PROCEDURE [dbo].[AccessRequest_CreateApprovedExtension]
    @AccessRequestId UNIQUEIDENTIFIER,
    @AccessDecisionId UNIQUEIDENTIFIER,
    @ExtensionOfLeaseId UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @CollectionId UNIQUEIDENTIFIER,
    @CipherId UNIQUEIDENTIFIER,
    @RequesterId UNIQUEIDENTIFIER,
    @NotBefore DATETIME2(7),
    @NotAfter DATETIME2(7),
    @Reason NVARCHAR(MAX) = NULL,
    @MaxExtensions INT,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON
    SET XACT_ABORT ON

    BEGIN TRANSACTION

    IF NOT EXISTS (
        SELECT 1
        FROM [dbo].[AccessLease] WITH (UPDLOCK, HOLDLOCK)
        WHERE [Id] = @ExtensionOfLeaseId
            AND [RequesterId] = @RequesterId
            AND [Status] = 0 /* Active */
            AND [NotAfter] > @Now
    )
    BEGIN
        ROLLBACK TRANSACTION
        SELECT 0 -- LeaseNotActive
        RETURN
    END

    IF (SELECT COUNT(*) FROM [dbo].[AccessRequest] WHERE [ExtensionOfLeaseId] = @ExtensionOfLeaseId) >= @MaxExtensions
    BEGIN
        ROLLBACK TRANSACTION
        SELECT -1 -- MaxExtensionsReached
        RETURN
    END

    INSERT INTO [dbo].[AccessRequest]
    (
        [Id], [ExtensionOfLeaseId], [OrganizationId], [CollectionId], [CipherId], [RequesterId],
        [NotBefore], [NotAfter], [Reason], [Status], [CreationDate], [ResolvedDate]
    )
    VALUES
    (
        @AccessRequestId, @ExtensionOfLeaseId, @OrganizationId, @CollectionId, @CipherId, @RequesterId,
        @NotBefore, @NotAfter, @Reason, 1 /* Approved */, @Now, @Now
    )

    INSERT INTO [dbo].[AccessDecision]
    (
        [Id], [AccessRequestId], [DeciderKind], [ApproverId], [ConditionKind],
        [Verdict], [Comment], [EvaluationContext], [CreationDate]
    )
    VALUES
    (
        @AccessDecisionId, @AccessRequestId, 0 /* Automatic */, NULL, NULL,
        0 /* Approve */, NULL, NULL, @Now
    )

    UPDATE [dbo].[AccessLease]
    SET [NotAfter] = @NotAfter
    WHERE [Id] = @ExtensionOfLeaseId

    COMMIT TRANSACTION

    SELECT 1 -- Extended
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessRequest_CountExtensionsByLeaseId]
    @LeaseId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT COUNT(*)
    FROM [dbo].[AccessRequest]
    WHERE [ExtensionOfLeaseId] = @LeaseId
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessRequest_ReadActiveApprovedByRequesterIdCipherId]
    @RequesterId UNIQUEIDENTIFIER,
    @CipherId UNIQUEIDENTIFIER,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    SELECT TOP 1
        AR.*
    FROM
        [dbo].[AccessRequest] AR
    WHERE
        AR.[RequesterId] = @RequesterId
        AND AR.[CipherId] = @CipherId
        AND AR.[Status] = 1 -- Approved
        AND AR.[NotAfter] > @Now
        AND AR.[ExtensionOfLeaseId] IS NULL
        AND NOT EXISTS (SELECT 1 FROM [dbo].[AccessLease] AL WHERE AL.[AccessRequestId] = AR.[Id])
    ORDER BY
        AR.[CreationDate] DESC
END
GO
