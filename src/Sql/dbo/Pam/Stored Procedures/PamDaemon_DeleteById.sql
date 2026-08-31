CREATE PROCEDURE [dbo].[PamDaemon_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON
    -- DeleteDaemonCommand's cascade, in one transaction. Two FKs into this daemon are ON DELETE NO ACTION --
    -- PamDaemonTargetAssignment -> PamDaemon (Organization carries the only cascade path back to that table) and
    -- PamDaemon -> ApiKey -- so the assignments go before the daemon row, and the daemon row before its credential.
    SET XACT_ABORT ON

    DECLARE @Now DATETIME2(7) = GETUTCDATE()
    DECLARE @ApiKeyId UNIQUEIDENTIFIER

    BEGIN TRANSACTION

    -- The stored row decides which credential goes; the caller's ApiKeyId is not trusted.
    SELECT @ApiKeyId = [ApiKeyId]
    FROM [dbo].[PamDaemon]
    WHERE [Id] = @Id

    -- PamRotationJob.ClaimedByDaemonId has no FK back to PamDaemon, and PamRotationJob_ReleaseExpiredLeases inner
    -- joins PamDaemon to find stale claimants -- so a job still claimed when its daemon disappears becomes invisible
    -- to the release sweep and only clears at PamRotationJob_TimeoutDue's much later ExpiresAt, blocking any
    -- replacement job for that config in the meantime. Release them here instead, while the claim is still visible.
    UPDATE AT
    SET AT.[Status] = 3, -- Abandoned
        AT.[ResolvedDate] = @Now
    FROM [dbo].[PamRotationAttempt] AT
    INNER JOIN [dbo].[PamRotationJob] J ON J.[Id] = AT.[JobId]
    WHERE AT.[Status] = 0 -- Executing
        AND J.[ClaimedByDaemonId] = @Id
        AND J.[Status] = 1 -- Claimed

    UPDATE [dbo].[PamRotationJob]
    SET [Status] = 0, -- Pending
        [ClaimedByDaemonId] = NULL,
        [ClaimedAt] = NULL,
        [NextClaimableAt] = @Now
    WHERE [ClaimedByDaemonId] = @Id
        AND [Status] = 1 -- Claimed

    DELETE FROM [dbo].[PamDaemonTargetAssignment]
    WHERE [DaemonId] = @Id

    DELETE FROM [dbo].[PamDaemon]
    WHERE [Id] = @Id

    DELETE FROM [dbo].[ApiKey]
    WHERE [Id] = @ApiKeyId

    COMMIT TRANSACTION
END
