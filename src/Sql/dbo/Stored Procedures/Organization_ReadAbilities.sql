CREATE PROCEDURE [dbo].[Organization_ReadAbilities]
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        [Id],
        [UseEvents],
        [Enabled]
    FROM
        [dbo].[Organization]
END