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