CREATE PROCEDURE [dbo].[PlayData_ReadByPlayId]
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
        [dbo].[PlayData]
    WHERE
        [PlayId] = @PlayId
END
