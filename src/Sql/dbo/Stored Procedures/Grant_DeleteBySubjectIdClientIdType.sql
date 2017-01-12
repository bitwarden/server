CREATE PROCEDURE [dbo].[Grant_DeleteBySubjectIdClientIdType]
    @SubjectId NVARCHAR(50),
    @ClientId NVARCHAR(50),
    @Type NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[Grant]
    WHERE
        [SubjectId] = @SubjectId
        AND [ClientId] = @ClientId
        AND [Type] = @Type
END