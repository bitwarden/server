CREATE PROCEDURE [dbo].[PlayData_DeleteByPlayId]
    @PlayId NVARCHAR(256)
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[PlayData]
    WHERE
        [PlayId] = @PlayId
END
