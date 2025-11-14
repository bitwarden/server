-- Populate MaxStorageGbIncreased for Users
-- Set MaxStorageGbIncreased = MaxStorageGb + 4 for all users with storage quota
UPDATE [dbo].[User]
SET [MaxStorageGbIncreased] = [MaxStorageGb] + 4
WHERE [MaxStorageGb] IS NOT NULL;
GO

-- Populate MaxStorageGbIncreased for Organizations
-- Set MaxStorageGbIncreased = MaxStorageGb + 4 for all organizations with storage quota
UPDATE [dbo].[Organization]
SET [MaxStorageGbIncreased] = [MaxStorageGb] + 4
WHERE [MaxStorageGb] IS NOT NULL;
GO
