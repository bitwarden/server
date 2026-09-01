CREATE PROCEDURE [dbo].[PamDaemon_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT *
    FROM [dbo].[PamDaemon]
    WHERE [Id] = @Id
END
