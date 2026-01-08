CREATE PROCEDURE [dbo].[PlayItem_ReadByPlayId]
    @PlayId NVARCHAR(256)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        [Id],
        [PlayId],
        [UserId],
        [OrganizationId],
        [CreationDate]
    FROM
        [dbo].[PlayItem]
    WHERE
        [PlayId] = @PlayId
END
