CREATE PROCEDURE [dbo].[PamRotationJob_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT *
    FROM [dbo].[PamRotationJob]
    WHERE [Id] = @Id
END
