CREATE PROCEDURE [dbo].[PamRotationConfig_DeleteWithJobs]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON
    -- DeleteRotationConfigCommand's cascade: the audit trail (AccessAuditEvent) is the durable history of a config's
    -- rotations, so jobs/attempts are hard-deleted here rather than soft-retired. Order matters -- attempts reference
    -- jobs, jobs reference the config, and both FKs are ON DELETE NO ACTION -- so children must go first.
    SET XACT_ABORT ON

    BEGIN TRANSACTION

    -- The caller's HasActiveJob read happened outside this transaction, so re-check under the same range lock
    -- PamRotationJob_Create takes. Without it a job created and claimed in the window is hard-deleted mid-rotation:
    -- the daemon changes the password on the target, then its accept-write and success report both find nothing,
    -- leaving the vault holding the old secret with no attempt row to record the drift.
    IF EXISTS (
        SELECT 1
        FROM [dbo].[PamRotationJob] WITH (UPDLOCK, HOLDLOCK)
        WHERE [RotationConfigId] = @Id
            AND [Status] IN (0, 1) -- Pending, Claimed
    )
    BEGIN
        ROLLBACK TRANSACTION
        SELECT 0 -- ActiveJobExists
        RETURN
    END

    DELETE A
    FROM [dbo].[PamRotationAttempt] A
    INNER JOIN [dbo].[PamRotationJob] J ON J.[Id] = A.[JobId]
    WHERE J.[RotationConfigId] = @Id

    DELETE FROM [dbo].[PamRotationJob]
    WHERE [RotationConfigId] = @Id

    DELETE FROM [dbo].[PamRotationConfig]
    WHERE [Id] = @Id

    COMMIT TRANSACTION

    SELECT 1 -- Deleted
END
