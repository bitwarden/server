CREATE PROCEDURE [dbo].[PamTargetSystem_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    -- No cascade cleanup here: a target system with rotation configs or daemon assignments still referencing it is
    -- blocked by their NO ACTION FKs (detach or delete those first). Deleting an already-gone row is a no-op.
    DELETE FROM [dbo].[PamTargetSystem]
    WHERE [Id] = @Id
END
