CREATE PROCEDURE [dbo].[PamDaemon_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON
    -- DeleteDaemonCommand's cascade: the PamDaemonTargetAssignment -> PamDaemon FK is ON DELETE NO ACTION
    -- (Organization carries the only cascade path back to that table), so a daemon that still has target assignments
    -- cannot be deleted until they are removed. Clear them here in the same transaction so deleting a daemon does not
    -- require the caller to unassign first. The daemon's dbo.ApiKey credential is deleted separately by the command
    -- (its FK is NO ACTION too, so this row must go first).
    SET XACT_ABORT ON

    DECLARE @Now DATETIME2(7) = GETUTCDATE()

    BEGIN TRANSACTION

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

    COMMIT TRANSACTION
END
