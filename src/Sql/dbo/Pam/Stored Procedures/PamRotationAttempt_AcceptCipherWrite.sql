CREATE PROCEDURE [dbo].[PamRotationAttempt_AcceptCipherWrite]
    @AttemptId UNIQUEIDENTIFIER,
    @DaemonId UNIQUEIDENTIFIER,
    @CipherData NVARCHAR(MAX),
    @LastKnownRevisionDate DATETIME2(7),
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON
    -- AcceptCipherUpdate's atomic write-capability check (security finding, plan §1): the job row is locked here
    -- WITH (UPDLOCK) for the life of the transaction, so a concurrent release/timeout sweep -- which updates the same
    -- job row -- blocks until this commits (or vice versa), closing the check-then-act window between "is this
    -- attempt still allowed to write" and "write the cipher". XACT_ABORT guarantees rollback (and a clean pooled
    -- connection) on any error.
    SET XACT_ABORT ON

    BEGIN TRANSACTION

    DECLARE @CipherId UNIQUEIDENTIFIER
    DECLARE @VerifiedJobId UNIQUEIDENTIFIER

    SELECT
        @CipherId = C.[CipherId],
        @VerifiedJobId = J.[Id]
    FROM [dbo].[PamRotationAttempt] AT
    INNER JOIN [dbo].[PamRotationJob] J WITH (UPDLOCK) ON J.[Id] = AT.[JobId]
    INNER JOIN [dbo].[PamRotationConfig] C ON C.[Id] = J.[RotationConfigId]
    WHERE AT.[Id] = @AttemptId
        AND AT.[Status] = 0 -- Executing
        AND AT.[ClaimedByDaemonId] = @DaemonId
        AND J.[Status] = 1 -- Claimed
        AND J.[ClaimedByDaemonId] = @DaemonId

    IF @VerifiedJobId IS NULL
    BEGIN
        -- The complement of spec AcceptCipherUpdate: unknown attempt, wrong claimant, or the job/attempt has already
        -- moved on (released/timed out/resolved). Audited by the caller as write_rejected.
        ROLLBACK TRANSACTION
        SELECT 0 -- Rejected
        RETURN
    END

    -- Outside RejectCipherUpdate's exact complement (plan §10 divergence): a drifted LastKnownRevisionDate means the
    -- vault item changed since the daemon last read it, so the write is rejected to protect a concurrent user edit
    -- rather than silently clobbering it. The 1-second tolerance mirrors CipherService's own last-known-revision
    -- check.
    IF ABS(DATEDIFF(MILLISECOND, (SELECT [RevisionDate] FROM [dbo].[Cipher] WHERE [Id] = @CipherId), @LastKnownRevisionDate)) > 1000
    BEGIN
        ROLLBACK TRANSACTION
        SELECT -1 -- RevisionMismatch
        RETURN
    END

    UPDATE [dbo].[Cipher]
    SET [Data] = @CipherData,
        [RevisionDate] = @Now
    WHERE [Id] = @CipherId

    UPDATE [dbo].[PamRotationAttempt]
    SET [CipherUpdated] = 1
    WHERE [Id] = @AttemptId

    COMMIT TRANSACTION

    SELECT 1 -- Accepted
END
