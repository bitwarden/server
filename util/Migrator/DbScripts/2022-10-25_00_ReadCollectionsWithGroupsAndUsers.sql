-- Stored Procedure: CollectionGroup_ReadByCollectionId
IF OBJECT_ID('[dbo].[CollectionGroup_ReadByCollectionId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[CollectionGroup_ReadByCollectionId]
END
GO

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

-- Stored Procedure: Collection_ReadWithGroupsAndUsersById
IF OBJECT_ID('[dbo].[Collection_ReadWithGroupsAndUsersById]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Collection_ReadWithGroupsAndUsersById]
END
GO

CREATE PROCEDURE [dbo].[Collection_ReadWithGroupsAndUsersById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[Collection_ReadById] @Id

    EXEC [dbo].[CollectionGroup_ReadByCollectionId] @Id

    EXEC [dbo].[CollectionUser_ReadByCollectionId] @Id
END

-- Stored Procedure: Collection_ReadWithGroupsAndUsersByIdUserId
IF OBJECT_ID('[dbo].[Collection_ReadWithGroupsAndUsersByIdUserId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Collection_ReadWithGroupsAndUsersByIdUserId]
END
GO

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