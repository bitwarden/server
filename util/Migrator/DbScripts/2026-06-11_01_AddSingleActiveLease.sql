-- PAM Credential Leasing: per-cipher single-active-lease.
--
-- AccessRule gains a [SingleActiveLease] flag. When the governing rule(s) ask for it, at most one active in-window
-- lease may exist for a given cipher across all users. The flag binds for a member only when EVERY collection through
-- which they reach the cipher is governed by a singleton rule (the union/OR gating logic lives in C#); any ungated or
-- non-singleton path is an escape that leaves them unconstrained. The mint procs receive @EnforceSingleActiveLease and
-- serialize concurrent activations of the same cipher with a UPDLOCK, HOLDLOCK range lock, returning a distinct
-- outcome code: 1 = minted, 0 = precondition fail, -1 = single-active conflict.
--
-- PAM is an unshipped POC behind the pm-37044-pam-v-0 flag with no production data; server + migration deploy
-- together, so the affected procs are altered in place rather than versioned.

IF COL_LENGTH('[dbo].[AccessRule]', 'SingleActiveLease') IS NULL
BEGIN
    ALTER TABLE [dbo].[AccessRule]
        ADD [SingleActiveLease] BIT NOT NULL
            CONSTRAINT [DF_AccessRule_SingleActiveLease] DEFAULT (0)
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessRule_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @Name NVARCHAR(256),
    @Description NVARCHAR(MAX) = NULL,
    @Conditions NVARCHAR(MAX),
    @SingleActiveLease BIT = 0,
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    INSERT INTO [dbo].[AccessRule]
    (
        [Id],
        [OrganizationId],
        [Name],
        [Description],
        [Conditions],
        [SingleActiveLease],
        [CreationDate],
        [RevisionDate]
    )
    VALUES
    (
        @Id,
        @OrganizationId,
        @Name,
        @Description,
        @Conditions,
        @SingleActiveLease,
        @CreationDate,
        @RevisionDate
    )
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessRule_Update]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER,
    @Name NVARCHAR(256),
    @Description NVARCHAR(MAX) = NULL,
    @Conditions NVARCHAR(MAX),
    @SingleActiveLease BIT = 0,
    @CreationDate DATETIME2(7),
    @RevisionDate DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        [dbo].[AccessRule]
    SET
        [OrganizationId] = @OrganizationId,
        [Name] = @Name,
        [Description] = @Description,
        [Conditions] = @Conditions,
        [SingleActiveLease] = @SingleActiveLease,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
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
    -- An explicit transaction is required so the singleton guard's range lock is held until the INSERT commits;
    -- XACT_ABORT guarantees the transaction is rolled back (and the pooled connection left clean) if the
    -- unique-index backstop [IX_AccessLease_AccessRequestId] trips on a concurrent activation of the same request.
    SET XACT_ABORT ON

    BEGIN TRANSACTION

    -- Per-cipher singleton guard. When the governing rule(s) ask for a single active lease, activation is allowed
    -- only if no other active in-window lease exists for the same cipher across all users. The UPDLOCK, HOLDLOCK
    -- range lock is held for the life of this transaction, so it serializes against the INSERT below: a concurrent
    -- same-cipher activation blocks here until this transaction commits, then sees the new lease and is rejected.
    -- Outcome -1 is distinct from the precondition-fail outcome (0) so the caller can surface a 409 conflict.
    IF @EnforceSingleActiveLease = 1
        AND EXISTS (
            SELECT 1
            FROM [dbo].[AccessLease] WITH (UPDLOCK, HOLDLOCK)
            WHERE [CipherId] = (SELECT [CipherId] FROM [dbo].[AccessRequest] WHERE [Id] = @AccessRequestId)
                AND [Status] = 0 /* Active */
                AND [NotBefore] <= @Now
                AND [NotAfter] > @Now
        )
    BEGIN
        ROLLBACK TRANSACTION
        SELECT -1
        RETURN
    END

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

    DECLARE @Rows INT = @@ROWCOUNT

    COMMIT TRANSACTION

    -- 1 = minted, 0 = precondition no longer held (caller re-reads the winner).
    SELECT CASE WHEN @Rows = 1 THEN 1 ELSE 0 END
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessLease_CreateAutoApproved]
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
    @Now DATETIME2(7),
    @EnforceSingleActiveLease BIT = 0
AS
BEGIN
    SET NOCOUNT ON

    BEGIN TRANSACTION AccessLease_CreateAutoApproved

    -- Per-cipher singleton guard. When the governing rule(s) ask for a single active lease, the auto-approved lease
    -- is minted only if no other active in-window lease exists for the same cipher across all users. The check runs
    -- inside the transaction before any insert so the UPDLOCK, HOLDLOCK range lock serializes concurrent activations
    -- of the same cipher; a conflict rolls back leaving nothing persisted and returns -1.
    IF @EnforceSingleActiveLease = 1
    BEGIN
        IF EXISTS (
            SELECT 1
            FROM [dbo].[AccessLease] WITH (UPDLOCK, HOLDLOCK)
            WHERE [CipherId] = @CipherId
                AND [Status] = 0 /* Active */
                AND [NotBefore] <= @Now
                AND [NotAfter] > @Now
        )
        BEGIN
            ROLLBACK TRANSACTION AccessLease_CreateAutoApproved
            SELECT -1
            RETURN
        END
    END

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

    -- 1 = minted (request + decision + lease all written).
    SELECT 1
END
GO
