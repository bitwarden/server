CREATE PROCEDURE [dbo].[PamRotationAttempt_AcceptCipherWrite]
    @AttemptId UNIQUEIDENTIFIER,
    @AccessConnectorId UNIQUEIDENTIFIER,
    @CipherData NVARCHAR(MAX),
    @LastKnownRevisionDate DATETIME2(7),
    @Now DATETIME2(7)
AS
BEGIN
    SET NOCOUNT ON
    -- WITH (UPDLOCK) closes the check-then-act window before the cipher write.
    SET XACT_ABORT ON

    BEGIN TRANSACTION

    DECLARE @CipherId UNIQUEIDENTIFIER
    DECLARE @OrganizationId UNIQUEIDENTIFIER
    DECLARE @VerifiedJobId UNIQUEIDENTIFIER

    SELECT
        @CipherId = C.[CipherId],
        @OrganizationId = C.[OrganizationId],
        @VerifiedJobId = J.[Id]
    FROM [dbo].[PamRotationAttempt] AT
    INNER JOIN [dbo].[PamRotationJob] J WITH (UPDLOCK) ON J.[Id] = AT.[JobId]
    INNER JOIN [dbo].[PamRotationConfig] C ON C.[Id] = J.[RotationConfigId]
    WHERE AT.[Id] = @AttemptId
        AND AT.[Action] = 0 -- None
        AND AT.[ClaimedByAccessConnectorId] = @AccessConnectorId
        AND J.[Action] = 1 -- Claimed
        AND J.[ExpiresAt] > @Now
        AND J.[ClaimedByAccessConnectorId] = @AccessConnectorId
        AND J.[ClaimedAt] = AT.[CreationDate]

    IF @VerifiedJobId IS NULL
    BEGIN
        -- Unknown attempt, wrong claimant, or an already-resolved job/attempt; caller audits as write_rejected.
        ROLLBACK TRANSACTION
        SELECT 0 -- Rejected
        RETURN
    END

    DECLARE @CipherRevisionDate DATETIME2(7) = (SELECT [RevisionDate] FROM [dbo].[Cipher] WHERE [Id] = @CipherId)

    -- The config has no FK to Cipher, so the cipher may have been hard-deleted.
    IF @CipherRevisionDate IS NULL
    BEGIN
        ROLLBACK TRANSACTION
        SELECT 0 -- Rejected
        RETURN
    END

    -- Drifted LastKnownRevisionDate means a concurrent edit; rejected, matching CipherService's tolerance.
    IF ABS(DATEDIFF_BIG(MILLISECOND, @CipherRevisionDate, @LastKnownRevisionDate)) > 1000
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

    -- Other writers of dbo.Cipher bump here too, so clients do not keep a stale password.
    EXEC [dbo].[User_BumpAccountRevisionDateByCipherId] @CipherId, @OrganizationId

    COMMIT TRANSACTION

    SELECT 1 -- Accepted
END
