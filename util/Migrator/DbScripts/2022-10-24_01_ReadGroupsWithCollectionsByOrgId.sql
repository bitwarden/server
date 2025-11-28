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
    
IF OBJECT_ID('[dbo].[User_BumpAccountRevisionDateByOrganizationIds]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[User_BumpAccountRevisionDateByOrganizationIds];
END
GO

CREATE PROCEDURE [dbo].[User_BumpAccountRevisionDateByOrganizationIds]
    @OrganizationIds AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

UPDATE
    U
SET
    U.[AccountRevisionDate] = GETUTCDATE()
    FROM
        [dbo].[User] U
    INNER JOIN
        [dbo].[OrganizationUser] OU ON OU.[UserId] = U.[Id]
WHERE
    OU.[OrganizationId] IN (SELECT [Id] FROM @OrganizationIds)
  AND OU.[Status] = 2 -- Confirmed
END
GO

IF OBJECT_ID('[dbo].[Group_DeleteByIds]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Group_DeleteByIds];
END
GO

CREATE PROCEDURE [dbo].[Group_DeleteByIds]
    @Ids AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON
        
    DECLARE @OrgIds AS [dbo].[GuidIdArray]

    INSERT INTO @OrgIds (Id)
    SELECT
        [OrganizationId]
    FROM
        [dbo].[Group]
    WHERE
        [Id] in (SELECT [Id] FROM @Ids)
    GROUP BY
        [OrganizationId]
    
    DECLARE @BatchSize INT = 100
        
    WHILE @BatchSize > 0
    BEGIN
        BEGIN TRANSACTION Group_DeleteMany_Groups
            DELETE TOP(@BatchSize)
            FROM
                [dbo].[Group]
            WHERE
                [Id] IN (SELECT [Id] FROM @Ids)
                
            SET @BatchSize = @@ROWCOUNT
        COMMIT TRANSACTION Group_DeleteMany_Groups
    END

    EXEC [dbo].[User_BumpAccountRevisionDateByOrganizationIds] @OrgIds
END