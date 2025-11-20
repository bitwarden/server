 -- ========================================
  -- Dependency Validation
  -- ========================================
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

  -- ========================================
  -- User Table Migration
  -- ========================================

  -- Create temporary index for performance
  IF NOT EXISTS (
      SELECT 1
      FROM sys.indexes
      WHERE object_id = OBJECT_ID('dbo.User')
      AND name = 'IX_TEMP_User_MaxStorageGb_MaxStorageGbIncreased'
  )
  BEGIN
      PRINT 'Creating temporary index on User table...';
      CREATE INDEX IX_TEMP_User_MaxStorageGb_MaxStorageGbIncreased
      ON [dbo].[User]([MaxStorageGb], [MaxStorageGbIncreased]);
      PRINT 'Temporary index created.';
  END
  GO

  -- Populate MaxStorageGbIncreased for Users in batches
  DECLARE @BatchSize INT = 5000;
  DECLARE @RowsAffected INT = 1;
  DECLARE @TotalUpdated INT = 0;

  PRINT 'Starting User table update...';

  WHILE @RowsAffected > 0
  BEGIN
      UPDATE TOP (@BatchSize) [dbo].[User]
      SET [MaxStorageGbIncreased] = [MaxStorageGb] + 4
      WHERE [MaxStorageGb] IS NOT NULL
        AND [MaxStorageGbIncreased] IS NULL;

      SET @RowsAffected = @@ROWCOUNT;
      SET @TotalUpdated = @TotalUpdated + @RowsAffected;

      PRINT 'Users updated: ' + CAST(@TotalUpdated AS VARCHAR(10));

      WAITFOR DELAY '00:00:00.100'; -- 100ms delay to reduce contention
  END

  PRINT 'User table update complete. Total rows updated: ' + CAST(@TotalUpdated AS VARCHAR(10));
  GO

  -- Drop temporary index
  DROP INDEX IF EXISTS [dbo].[User].[IX_TEMP_User_MaxStorageGb_MaxStorageGbIncreased];
  PRINT 'Temporary index on User table dropped.';
  GO

  -- ========================================
  -- Organization Table Migration
  -- ========================================

  -- Create temporary index for performance
  IF NOT EXISTS (
      SELECT 1
      FROM sys.indexes
      WHERE object_id = OBJECT_ID('dbo.Organization')
      AND name = 'IX_TEMP_Organization_MaxStorageGb_MaxStorageGbIncreased'
  )
  BEGIN
      PRINT 'Creating temporary index on Organization table...';
      CREATE INDEX IX_TEMP_Organization_MaxStorageGb_MaxStorageGbIncreased
      ON [dbo].[Organization]([MaxStorageGb], [MaxStorageGbIncreased]);
      PRINT 'Temporary index created.';
  END
  GO

  -- Populate MaxStorageGbIncreased for Organizations in batches
  DECLARE @BatchSize INT = 5000;
  DECLARE @RowsAffected INT = 1;
  DECLARE @TotalUpdated INT = 0;

  PRINT 'Starting Organization table update...';

  WHILE @RowsAffected > 0
  BEGIN
      UPDATE TOP (@BatchSize) [dbo].[Organization]
      SET [MaxStorageGbIncreased] = [MaxStorageGb] + 4
      WHERE [MaxStorageGb] IS NOT NULL
        AND [MaxStorageGbIncreased] IS NULL;

      SET @RowsAffected = @@ROWCOUNT;
      SET @TotalUpdated = @TotalUpdated + @RowsAffected;

      PRINT 'Organizations updated: ' + CAST(@TotalUpdated AS VARCHAR(10));

      WAITFOR DELAY '00:00:00.100'; -- 100ms delay to reduce contention
  END

  PRINT 'Organization table update complete. Total rows updated: ' + CAST(@TotalUpdated AS VARCHAR(10));
  GO

  -- Drop temporary index
  DROP INDEX IF EXISTS [dbo].[Organization].[IX_TEMP_Organization_MaxStorageGb_MaxStorageGbIncreased];
  PRINT 'Temporary index on Organization table dropped.';
  GO