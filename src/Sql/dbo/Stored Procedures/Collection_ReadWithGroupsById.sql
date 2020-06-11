CREATE PROCEDURE [dbo].[Collection_ReadWithGroupsById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[Collection_ReadById] @Id

    SELECT
        [GroupId] [Id],
        [ReadOnly],
        [HidePasswords]
    FROM
        [dbo].[CollectionGroup]
    WHERE
        [CollectionId] = @Id
END