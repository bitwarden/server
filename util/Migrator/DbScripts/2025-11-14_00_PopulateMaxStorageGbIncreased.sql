  -- Dependency validation: Ensure PM-27603 has been deployed
  -- This prevents cryptic "Invalid column name" errors

  IF NOT EXISTS (
      SELECT 1 FROM sys.columns
      WHERE object_id = OBJECT_ID('[dbo].[User]')
      AND name = 'MaxStorageGbIncreased'
  )
  BEGIN
      RAISERROR('MaxStorageGbIncreased column does not exist in User table. PM-27603 must be deployed first.', 16, 1);
      RETURN;
  END;

  IF NOT EXISTS (
      SELECT 1 FROM sys.columns
      WHERE object_id = OBJECT_ID('[dbo].[Organization]')
      AND name = 'MaxStorageGbIncreased'
  )
  BEGIN
      RAISERROR('MaxStorageGbIncreased column does not exist in Organization table. PM-27603 must be deployed first.', 16, 1);
      RETURN;
  END;
  GO
  
-- Populate MaxStorageGbIncreased for Users in batches
-- Set MaxStorageGbIncreased = MaxStorageGb + 4 for all users with storage quota
-- Using batched updates to reduce lock contention and transaction log impact
DECLARE @BatchSize INT = 5000;
DECLARE @RowsAffected INT = 1;
DECLARE @TotalUpdated INT = 0;

PRINT 'Starting User table update...';

WHILE @RowsAffected > 0
BEGIN
    UPDATE TOP (@BatchSize) [dbo].[User]
    SET [MaxStorageGbIncreased] = [MaxStorageGb] + 4
    WHERE [MaxStorageGb] IS NOT NULL
      AND [MaxStorageGbIncreased] IS NULL; -- Only update rows not yet processed

    SET @RowsAffected = @@ROWCOUNT;
    SET @TotalUpdated = @TotalUpdated + @RowsAffected;

    PRINT 'Users updated: ' + CAST(@TotalUpdated AS VARCHAR(10));

    WAITFOR DELAY '00:00:00.100'; -- 100ms delay to reduce contention
END

PRINT 'User table update complete. Total rows updated: ' + CAST(@TotalUpdated AS VARCHAR(10));
GO

-- Populate MaxStorageGbIncreased for Organizations in batches
-- Set MaxStorageGbIncreased = MaxStorageGb + 4 for all organizations with storage quota
-- Using batched updates to reduce lock contention and transaction log impact
DECLARE @BatchSize INT = 5000;
DECLARE @RowsAffected INT = 1;
DECLARE @TotalUpdated INT = 0;

PRINT 'Starting Organization table update...';

WHILE @RowsAffected > 0
BEGIN
    UPDATE TOP (@BatchSize) [dbo].[Organization]
    SET [MaxStorageGbIncreased] = [MaxStorageGb] + 4
    WHERE [MaxStorageGb] IS NOT NULL
      AND [MaxStorageGbIncreased] IS NULL; -- Only update rows not yet processed

    SET @RowsAffected = @@ROWCOUNT;
    SET @TotalUpdated = @TotalUpdated + @RowsAffected;

    PRINT 'Organizations updated: ' + CAST(@TotalUpdated AS VARCHAR(10));

    WAITFOR DELAY '00:00:00.100'; -- 100ms delay to reduce contention
END

PRINT 'Organization table update complete. Total rows updated: ' + CAST(@TotalUpdated AS VARCHAR(10));
GO
