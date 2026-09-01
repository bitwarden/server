CREATE PROCEDURE [dbo].[PamRotationConfig_ReadByCipherId]
    @CipherId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    -- OneConfigPerCipher: at most one row can ever match.
    SELECT *
    FROM [dbo].[PamRotationConfig]
    WHERE [CipherId] = @CipherId
END
