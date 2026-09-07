-- Lets an admin permanently delete a target system; Disable stays the reversible control.
-- Guards against referencing configs; cascades assignments under a re-checked range lock.
-- The generic repository delete is left alone, still blocked by the NO ACTION FKs.
CREATE OR ALTER PROCEDURE [dbo].[PamRotationConfig_AnyByTargetSystem]
    @TargetSystemId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    -- Guard read: refuses a delete while any config still targets it.
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
    -- Cascade deletes assignments with the target; a rotation config instead blocks the delete.
    SET XACT_ABORT ON

    BEGIN TRANSACTION

    -- Re-checks under a range lock, since the caller's guard read was outside this transaction.
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
