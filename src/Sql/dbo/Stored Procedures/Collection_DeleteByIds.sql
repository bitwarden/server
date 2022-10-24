CREATE PROCEDURE [dbo].[Collection_DeleteByIds]
    @Ids AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @BatchSize INT = 100
	
    -- Delete Collection Groups
    WHILE @BatchSize > 0
    BEGIN
        BEGIN TRANSACTION CollectionGroup_DeleteMany
        	DELETE TOP(@BatchSize) 
        	FROM
        		[dbo].[CollectionGroup]
        	WHERE
                [CollectionId] IN (SELECT [Id] FROM @Ids)


            SET @BatchSize = @@ROWCOUNT
        COMMIT TRANSACTION CollectionGroup_DeleteMany
    END
    
    -- Reset batch size
    SET @BatchSize = 100

    -- Delete Collections
    WHILE @BatchSize > 0
    BEGIN
	    BEGIN TRANSACTION Collection_DeleteMany
            DELETE TOP(@BatchSize)
            FROM
                [dbo].[Collection]
            WHERE
                [Id] IN (SELECT [Id] FROM @Ids)
            
            SET @BatchSize = @@ROWCOUNT
        COMMIT TRANSACTION CollectionGroup_DeleteMany
	END
END