CREATE PROCEDURE [dbo].[PamRotationConfig_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT *
    FROM [dbo].[PamRotationConfig]
    WHERE [Id] = @Id
END
