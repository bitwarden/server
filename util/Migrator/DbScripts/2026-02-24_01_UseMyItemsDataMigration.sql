-- Enable UseMyItems for all organizations with UsePolicies enabled
-- Batch to avoid table locks
DECLARE @BatchSize INT = 1000;
DECLARE @RowsAffected INT = 1;

WHILE @RowsAffected > 0
    BEGIN
        UPDATE TOP (@BatchSize) [dbo].[Organization]
        SET [UseMyItems] = 1
        WHERE [UsePolicies] = 1
          AND [UseMyItems] = 0;

        SET @RowsAffected = @@ROWCOUNT;
    END
