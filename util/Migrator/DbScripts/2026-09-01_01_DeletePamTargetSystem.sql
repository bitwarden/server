-- Let an admin permanently delete a PAM target system. Disable is the reversible control and stays the one to reach
-- for; a delete is for a target that has left the estate. Two procedures back it:
--
--   * [dbo].[PamRotationConfig_AnyByTargetSystem] -- the guard read. A rotation config is the credential's own
--     configuration, so a target with configs against it is refused; the admin deletes those first.
--   * [dbo].[PamTargetSystem_DeleteWithAssignments] -- the cascade. Access connector assignments are only the
--     connector-to-target edge and go with the target, inside a transaction that re-checks the config guard under a
--     range lock.
--
-- The pre-existing [dbo].[PamTargetSystem_DeleteById] is left alone: it is the generic repository's plain delete and
-- is still blocked outright by the NO ACTION FKs into the target.

CREATE OR ALTER PROCEDURE [dbo].[PamRotationConfig_AnyByTargetSystem]
    @TargetSystemId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    -- DeleteTargetSystemCommand's guard: a target may only be removed once nothing is configured against it. The
    -- delete itself re-checks under lock (PamTargetSystem_DeleteWithAssignments); this read is what turns the common
    -- case into a refusal the admin can act on before anything is audited.
    SELECT 1
    FROM [dbo].[PamRotationConfig]
    WHERE [TargetSystemId] = @TargetSystemId
END
GO

CREATE OR ALTER PROCEDURE [dbo].[PamTargetSystem_DeleteWithAssignments]
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
GO
