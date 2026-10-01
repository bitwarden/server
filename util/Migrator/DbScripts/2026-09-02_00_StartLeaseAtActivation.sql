-- Starts a lease at activation (@Now), not the approved window's start; NotAfter is unchanged.
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

    -- At most one active lease per cipher; UPDLOCK/HOLDLOCK serializes and rejects concurrent activation.
    -- Outcome -1 (guard) is distinct from 0 (precondition-fail); the caller maps -1 to 409.
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

    -- Mints from @Now, not AR.[NotBefore], to AR.[NotAfter]; late activation shortens the lease.
    -- Preconditions are restated as defense in depth; the claim above already holds the row.
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

    -- 1 = minted, 0 = precondition no longer held (caller re-reads the winner).
    SELECT CASE WHEN @Rows = 1 THEN 1 ELSE 0 END
END
GO
