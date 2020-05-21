CREATE PROCEDURE [dbo].[Group_ReadWithCollectionsById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[Group_ReadById] @Id

    SELECT
        [CollectionId] [Id],
        [ReadOnly],
        [HidePasswords]
    FROM
        [dbo].[CollectionGroup]
    WHERE
        [GroupId] = @Id
END