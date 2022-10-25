-- Stored Procedure: CollectionGroup_ReadByCollectionId
CREATE OR ALTER PROCEDURE [dbo].[CollectionGroup_ReadByCollectionId]
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
GO

-- Stored Procedure: Collection_ReadWithGroupsAndUsersById
CREATE OR ALTER PROCEDURE [dbo].[Collection_ReadWithGroupsAndUsersById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[Collection_ReadById] @Id

    EXEC [dbo].[CollectionGroup_ReadByCollectionId] @Id

    EXEC [dbo].[CollectionUser_ReadByCollectionId] @Id
END
GO

-- Stored Procedure: Collection_ReadWithGroupsAndUsersByIdUserId
CREATE OR ALTER PROCEDURE [dbo].[Collection_ReadWithGroupsAndUsersByIdUserId]
    @Id UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[Collection_ReadByIdUserId] @Id, @UserId

    EXEC [dbo].[CollectionGroup_ReadByCollectionId] @Id

    EXEC [dbo].[CollectionUser_ReadByCollectionId] @Id
END