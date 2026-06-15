-- PAM Credential Leasing: rework extension settings.
--
-- Product direction changed: a lease may now be extended exactly once, and the admin instead caps the *length* of
-- that extension. So AccessRule.[MaxExtensions] (a count) is dropped in favour of [MaxExtensionDurationSeconds] (the
-- longest a single extension may run), and AccessRequest_CreateApprovedExtension no longer takes @MaxExtensions —
-- it rejects when any extension request already exists for the lease.
--
-- Supersedes the column/proc shapes added in 2026-06-12_01/02 (kept intact so already-migrated dev DBs roll forward
-- cleanly). PAM is an unshipped POC behind the pm-37044-pam-v-0 flag with no production data; the affected procs are
-- altered in place.

IF COL_LENGTH('[dbo].[AccessRule]', 'MaxExtensions') IS NOT NULL
BEGIN
    ALTER TABLE [dbo].[AccessRule] DROP COLUMN [MaxExtensions]
END
GO

IF COL_LENGTH('[dbo].[AccessRule]', 'MaxExtensionDurationSeconds') IS NULL
BEGIN
    ALTER TABLE [dbo].[AccessRule] ADD [MaxExtensionDurationSeconds] INT NULL
END
GO

CREATE OR ALTER PROCEDURE [dbo].[AccessRule_Create]
    @Id UNIQUEIDENTIFIER OUTPUT,
    @OrganizationId UNIQUEIDENTIFIER,
    @Name NVARCHAR(256),
    @Description NVARCHAR(MAX) = NULL,
    @Conditions NVARCHAR(MAX),
    @SingleActiveLease BIT = 0,
    @DefaultLeaseDurationSeconds INT = NULL,
    @MaxLeaseDurationSeconds INT = NULL,
    @Enabled BIT = 1,
    @AllowsExtensions BIT = 0,
    @MaxExtensionDurationSeconds INT = NULL,
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
        [DefaultLeaseDurationSeconds],
        [MaxLeaseDurationSeconds],
        [Enabled],
        [AllowsExtensions],
        [MaxExtensionDurationSeconds],
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
        @DefaultLeaseDurationSeconds,
        @MaxLeaseDurationSeconds,
        @Enabled,
        @AllowsExtensions,
        @MaxExtensionDurationSeconds,
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
    @DefaultLeaseDurationSeconds INT = NULL,
    @MaxLeaseDurationSeconds INT = NULL,
    @Enabled BIT = 1,
    @AllowsExtensions BIT = 0,
    @MaxExtensionDurationSeconds INT = NULL,
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
        [DefaultLeaseDurationSeconds] = @DefaultLeaseDurationSeconds,
        [MaxLeaseDurationSeconds] = @MaxLeaseDurationSeconds,
        [Enabled] = @Enabled,
        [AllowsExtensions] = @AllowsExtensions,
        [MaxExtensionDurationSeconds] = @MaxExtensionDurationSeconds,
        [CreationDate] = @CreationDate,
        [RevisionDate] = @RevisionDate
    WHERE
        [Id] = @Id
END
GO

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

    -- A lease may be extended exactly once. Counted under the lease lock, so it is race-safe against a concurrent
    -- extension of the same lease.
    IF EXISTS (SELECT 1 FROM [dbo].[AccessRequest] WHERE [ExtensionOfLeaseId] = @ExtensionOfLeaseId)
    BEGIN
        ROLLBACK TRANSACTION
        SELECT -1 -- AlreadyExtended
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
