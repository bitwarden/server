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

    BEGIN TRANSACTION

    DELETE FROM [dbo].[PamDaemonTargetAssignment]
    WHERE [DaemonId] = @Id

    DELETE FROM [dbo].[PamDaemon]
    WHERE [Id] = @Id

    COMMIT TRANSACTION
END
