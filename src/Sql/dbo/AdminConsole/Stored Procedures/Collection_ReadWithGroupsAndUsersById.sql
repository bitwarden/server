CREATE PROCEDURE [dbo].[Collection_ReadWithGroupsAndUsersById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[Collection_ReadById] @Id

    EXEC [dbo].[CollectionGroup_ReadByCollectionId] @Id

    EXEC [dbo].[CollectionUser_ReadByCollectionId] @Id
END