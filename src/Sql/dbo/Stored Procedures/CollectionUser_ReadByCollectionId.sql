CREATE PROCEDURE [dbo].[CollectionUser_ReadByCollectionId]
    @CollectionId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        [OrganizationUserId] [Id],
        [ReadOnly]
    FROM
        [dbo].[CollectionUser]
    WHERE
        [CollectionId] = @CollectionId
END