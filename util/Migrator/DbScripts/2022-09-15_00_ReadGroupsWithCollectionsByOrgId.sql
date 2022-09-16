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
GO

IF OBJECT_ID('[dbo].[Group_ReadByIds]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Group_ReadByIds];
END
GO

CREATE PROCEDURE [dbo].[Group_ReadByIds]
    @Ids AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    IF (SELECT COUNT(1) FROM @Ids) < 1
        BEGIN
            RETURN(-1)
        END

    SELECT
        *
    FROM
        [dbo].[Group]
    WHERE
        [Id] IN (SELECT [Id] FROM @Ids)
END
GO

IF OBJECT_ID('[dbo].[Group_DeleteByIdsOrganizationId]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Group_DeleteByIdsOrganizationId];
END
GO

CREATE PROCEDURE [dbo].[Group_DeleteByIdsOrganizationId]
    @Ids AS [dbo].[GuidIdArray] READONLY,
    @OrganizationId AS UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON
        
    DECLARE @BatchSize INT = 100
        
    WHILE @BatchSize > 0
    BEGIN
        BEGIN TRANSACTION Group_DeleteMany_Groups
            DELETE TOP(@BatchSize)
            FROM
                [dbo].[Group]
            WHERE
                [Id] IN (SELECT [Id] FROM @Ids)
                AND [OrganizationId] = @OrganizationId
                
            SET @BatchSize = @@ROWCOUNT
        COMMIT TRANSACTION Group_DeleteMany_Groups
    END
    
    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationId] @OrganizationId
END