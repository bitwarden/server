CREATE PROCEDURE [dbo].[Collection_ReadWithGroupsAndUsersByIdUserId]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[Collection_ReadByIdUserId] @Id, @UserId

    EXEC [dbo].[CollectionGroup_ReadByCollectionId] @Id

    EXEC [dbo].[CollectionUser_ReadByCollectionId] @Id
END