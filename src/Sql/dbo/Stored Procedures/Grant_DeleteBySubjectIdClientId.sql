CREATE PROCEDURE [dbo].[Grant_DeleteBySubjectIdClientId]
    @SubjectId NVARCHAR(50),
    @ClientId NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[Grant]
    WHERE
        [SubjectId] = @SubjectId
        AND [ClientId] = @ClientId
END