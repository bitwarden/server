CREATE PROCEDURE [dbo].[PamTargetSystem_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT *
    FROM [dbo].[PamTargetSystem]
    WHERE [Id] = @Id
END
