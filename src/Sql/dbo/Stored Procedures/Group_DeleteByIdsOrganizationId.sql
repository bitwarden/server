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