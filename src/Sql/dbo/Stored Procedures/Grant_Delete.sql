CREATE PROCEDURE [dbo].[Grant_Delete]
    @SubjectId NVARCHAR(200),
    @SessionId NVARCHAR(100),
    @ClientId NVARCHAR(200),
    @Type NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[Grant]
    WHERE
        (@SubjectId IS NULL OR [SubjectId] = @SubjectId)
        AND (@ClientId IS NULL OR [ClientId] = @ClientId)
        AND (@SessionId IS NULL OR [SessionId] = @SessionId)
        AND (@Type IS NULL OR [Type] = @Type)
END
