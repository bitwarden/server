CREATE PROCEDURE [dbo].[PamRotationConfig_DeleteWithJobs]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON
    -- DeleteRotationConfigCommand's cascade: the audit trail (AccessAuditEvent) is the durable history of a config's
    -- rotations, so jobs/attempts are hard-deleted here rather than soft-retired. Order matters -- attempts reference
    -- jobs, jobs reference the config, and both FKs are ON DELETE NO ACTION -- so children must go first. The caller
    -- has already confirmed the config has no active job.
    SET XACT_ABORT ON

    BEGIN TRANSACTION

    DELETE A
    FROM [dbo].[PamRotationAttempt] A
    INNER JOIN [dbo].[PamRotationJob] J ON J.[Id] = A.[JobId]
    WHERE J.[RotationConfigId] = @Id

    DELETE FROM [dbo].[PamRotationJob]
    WHERE [RotationConfigId] = @Id

    DELETE FROM [dbo].[PamRotationConfig]
    WHERE [Id] = @Id

    COMMIT TRANSACTION
END
