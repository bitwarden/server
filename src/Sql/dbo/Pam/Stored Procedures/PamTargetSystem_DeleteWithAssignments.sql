CREATE PROCEDURE [dbo].[PamTargetSystem_DeleteWithAssignments]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON
    -- DeleteTargetSystemCommand's cascade: an access connector assignment is only the connector-to-target edge and
    -- means nothing once the target is gone, so it goes with the target -- whereas a rotation config is the
    -- credential's own configuration and blocks the delete instead. The assignment -> target FK is ON DELETE NO
    -- ACTION, so the assignments must go first.
    SET XACT_ABORT ON

    BEGIN TRANSACTION

    -- The caller's PamRotationConfig_AnyByTargetSystem read happened outside this transaction, so re-check under a
    -- range lock on IX_PamRotationConfig_TargetSystemId, which blocks a PamRotationConfig_Create for this target for
    -- the duration. Without it a config created in the window loses its target: the FK is NO ACTION, so the delete
    -- would fail outright, and were it not, the credential would silently stop rotating.
    IF EXISTS (
        SELECT 1
        FROM [dbo].[PamRotationConfig] WITH (UPDLOCK, HOLDLOCK)
        WHERE [TargetSystemId] = @Id
    )
    BEGIN
        ROLLBACK TRANSACTION
        SELECT 0 -- RotationConfigExists
        RETURN
    END

    DELETE FROM [dbo].[PamDaemonTargetAssignment]
    WHERE [TargetSystemId] = @Id

    DELETE FROM [dbo].[PamTargetSystem]
    WHERE [Id] = @Id

    COMMIT TRANSACTION

    SELECT 1 -- Deleted
END
