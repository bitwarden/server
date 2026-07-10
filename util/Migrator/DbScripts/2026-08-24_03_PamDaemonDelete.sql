-- PAM rotation: daemon hard-delete. Adds [dbo].[PamDaemon_DeleteById], which the generic
-- Repository<PamDaemon, Guid>.DeleteAsync convention invokes. It clears the daemon's target assignments (whose FK to
-- PamDaemon is ON DELETE NO ACTION) before deleting the daemon row, in one transaction; DeleteDaemonCommand then
-- deletes the daemon's dbo.ApiKey credential separately. This replaces the old revoke action (disable/enable + delete).

CREATE OR ALTER PROCEDURE [dbo].[PamDaemon_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON
    SET XACT_ABORT ON

    BEGIN TRANSACTION

    DELETE FROM [dbo].[PamDaemonTargetAssignment]
    WHERE [DaemonId] = @Id

    DELETE FROM [dbo].[PamDaemon]
    WHERE [Id] = @Id

    COMMIT TRANSACTION
END
GO
