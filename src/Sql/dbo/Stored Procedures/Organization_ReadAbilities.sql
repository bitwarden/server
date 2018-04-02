CREATE PROCEDURE [dbo].[Organization_ReadAbilities]
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        [Id],
        [UseEvents],
        [Use2fa],
        [Enabled]
    FROM
        [dbo].[Organization]
END