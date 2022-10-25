CREATE PROCEDURE [dbo].[CollectionGroup_ReadByCollectionId]
    @CollectionId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        [GroupId] [Id],
        [ReadOnly],
        [HidePasswords]
    FROM
        [dbo].[CollectionGroup]
    WHERE
        [CollectionId] = @CollectionId
END