-- PAM Credential Leasing: approval no longer mints the lease. A human approval now only records the verdict and
-- flips the request to Approved; the requester activates the approved request when they actually access the item
-- ([AccessLease_CreateFromApprovedRequest]), which is when the active lease is minted. The automatic path is
-- unchanged and still mints instantly via [AccessLease_CreateAutoApproved] — there the requester is online and
-- asking for access now.
--
-- [AccessRequest_ResolveWithDecision] loses its @AccessLeaseId parameter outright instead of keeping an ignored
-- default: during a mixed-binary window an old server passing @AccessLeaseId would error. Acceptable here — the
-- feature is an unshipped POC behind the pm-37044-pam-v-0 flag and server + migration deploy together.

CREATE OR ALTER PROCEDURE [dbo].[AccessRequest_ResolveWithDecision]
    @AccessRequestId UNIQUEIDENTIFIER,
    @Status TINYINT,
    @AccessDecisionId UNIQUEIDENTIFIER,
    @ApproverId UNIQUEIDENTIFIER,
    @Verdict TINYINT,
    @Comment NVARCHAR(MAX) = NULL,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- Atomically resolve a pending request and record the human approver's decision. The caller has already verified
    -- (and the application enforces) that the request is still Pending; the WHERE guard keeps the write idempotent
    -- under a race so a second approver can't move an already-resolved request.
    --
    -- Approval does not mint the lease: the requester activates the approved request later via
    -- [AccessLease_CreateFromApprovedRequest]. The automatic path still mints instantly via
    -- [AccessLease_CreateAutoApproved], where the requester is online and asking for access now.
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

    COMMIT TRANSACTION AccessRequest_Resolve
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessRequest_ReadActiveApprovedByRequesterIdCipherId]
    @RequesterId UNIQUEIDENTIFIER,
    @CipherId UNIQUEIDENTIFIER,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- The caller's approved-but-not-yet-activated request whose window can still produce access. Future windows are
    -- included (the client shows the upcoming window); lapsed windows are excluded so the client never offers an
    -- activation that the server would reject. A request that has produced a lease is activated, not approved.
    SELECT TOP 1
        AR.*
    FROM
        [dbo].[AccessRequest] AR
    WHERE
        AR.[RequesterId] = @RequesterId
        AND AR.[CipherId] = @CipherId
        AND AR.[Status] = 1 -- Approved
        AND AR.[NotAfter] > @Now
        AND NOT EXISTS (SELECT 1 FROM [dbo].[AccessLease] AL WHERE AL.[AccessRequestId] = AR.[Id])
    ORDER BY
        AR.[CreationDate] DESC
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessLease_ReadByAccessRequestId]
    @AccessRequestId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    -- A request produces at most one lease ([IX_AccessLease_AccessRequestId] is unique); TOP 1 is belt and braces.
    SELECT TOP 1
        *
    FROM
        [dbo].[AccessLease]
    WHERE
        [AccessRequestId] = @AccessRequestId
    ORDER BY
        [CreationDate] DESC
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessLease_CreateFromApprovedRequest]
    @AccessLeaseId UNIQUEIDENTIFIER,
    @AccessRequestId UNIQUEIDENTIFIER,
    @RequesterId UNIQUEIDENTIFIER,
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    -- Activation of an approved request: mints the active lease that authorizes access, spanning the request's
    -- approved window. Every application-level precondition is re-checked inside the INSERT so a concurrent
    -- activation cannot double-mint; zero rows inserted means a precondition no longer held and the caller decides
    -- how to surface that. [IX_AccessLease_AccessRequestId] (unique) is the backstop when two calls pass the
    -- NOT EXISTS check simultaneously.
    INSERT INTO [dbo].[AccessLease]
    (
        [Id], [AccessRequestId], [OrganizationId], [CollectionId], [CipherId], [RequesterId],
        [Status], [NotBefore], [NotAfter], [RevokedDate], [RevokedBy], [CreationDate]
    )
    SELECT
        @AccessLeaseId, AR.[Id], AR.[OrganizationId], AR.[CollectionId], AR.[CipherId], AR.[RequesterId],
        0 /* Active */, AR.[NotBefore], AR.[NotAfter], NULL, NULL, @Now
    FROM [dbo].[AccessRequest] AR
    WHERE
        AR.[Id] = @AccessRequestId
        AND AR.[RequesterId] = @RequesterId
        AND AR.[Status] = 1 -- Approved
        AND AR.[NotBefore] <= @Now
        AND AR.[NotAfter] > @Now
        AND NOT EXISTS (SELECT 1 FROM [dbo].[AccessLease] AL WHERE AL.[AccessRequestId] = AR.[Id])

    SELECT @@ROWCOUNT
END
GO

-- One lease per request, ever. Dev databases may hold duplicates from the earlier approval-mint race
-- ([AccessRequest_ResolveWithDecision] inserted the decision and lease even when the pending-guard UPDATE matched
-- zero rows); keep the earliest lease per request. No production data exists, and the server never writes
-- [AccessRequest].[ExtensionOfLeaseId] yet, so its FK cannot block these deletes.
DELETE AL
FROM [dbo].[AccessLease] AL
WHERE EXISTS (
    SELECT 1
    FROM [dbo].[AccessLease] AL2
    WHERE AL2.[AccessRequestId] = AL.[AccessRequestId]
        AND (AL2.[CreationDate] < AL.[CreationDate]
            OR (AL2.[CreationDate] = AL.[CreationDate] AND AL2.[Id] < AL.[Id]))
)
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.indexes
    WHERE [name] = 'IX_AccessLease_AccessRequestId'
        AND [object_id] = OBJECT_ID('[dbo].[AccessLease]')
)
BEGIN
    -- A request produces at most one lease, ever: activating an approved request and the automatic path each insert
    -- exactly one. Unique to backstop racing activations that pass the application-level checks simultaneously.
    CREATE UNIQUE NONCLUSTERED INDEX [IX_AccessLease_AccessRequestId]
        ON [dbo].[AccessLease] ([AccessRequestId] ASC);
END
GO
