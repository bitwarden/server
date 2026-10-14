CREATE PROCEDURE [dbo].[PamRotationConfig_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[PamRotationConfigView]
    WHERE
        [Id] = @Id
END
