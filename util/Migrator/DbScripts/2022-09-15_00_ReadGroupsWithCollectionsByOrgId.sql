IF OBJECT_ID('[dbo].[CollectionGroup_ReadByOrganizationId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[CollectionGroup_ReadByOrganizationId];
END
GO

CREATE PROCEDURE [dbo].[CollectionGroup_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

SELECT
    CG.*
FROM
    [dbo].[CollectionGroup] CG
    INNER JOIN
    [dbo].[Group] G ON G.[Id] = CG.[GroupId]
WHERE
    G.[OrganizationId] = @OrganizationId
END
GO

IF OBJECT_ID('[dbo].[Group_ReadWithCollectionsByOrganizationId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Group_ReadWithCollectionsByOrganizationId];
END
GO

CREATE PROCEDURE [dbo].[Group_ReadWithCollectionsByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[Group_ReadByOrganizationId] @OrganizationId

    EXEC [dbo].[CollectionGroup_ReadByOrganizationId] @OrganizationId
END