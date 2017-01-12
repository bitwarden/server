CREATE PROCEDURE [dbo].[Grant_ReadBySubjectId]
    @SubjectId NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[GrantView]
    WHERE
        [SubjectId] = @SubjectId
END