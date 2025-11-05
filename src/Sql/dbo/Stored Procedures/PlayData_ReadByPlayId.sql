CREATE PROCEDURE [dbo].[PlayData_ReadByPlayId]
    @PlayId NVARCHAR(256)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[PlayData]
    WHERE
        [PlayId] = @PlayId
END
