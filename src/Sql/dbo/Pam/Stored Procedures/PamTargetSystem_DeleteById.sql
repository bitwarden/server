CREATE PROCEDURE [dbo].[PamTargetSystem_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    -- No cascade; a referenced target is blocked by its NO ACTION FKs.
    DELETE FROM [dbo].[PamTargetSystem]
    WHERE [Id] = @Id
END
