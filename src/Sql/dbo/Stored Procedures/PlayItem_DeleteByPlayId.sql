CREATE PROCEDURE [dbo].[PlayItem_DeleteByPlayId]
    @PlayId NVARCHAR(256)
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[PlayItem]
    WHERE
        [PlayId] = @PlayId
END
