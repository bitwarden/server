/*
    Script to finalize the addition of MasterPasswordSalt to User table by making it NOT NULL.
    This script should be run only after all existing rows have been backfilled with non-NULL values from
    the transition script 2026-02-03_00_PopulateMasterPasswordSaltColumn.sql.
*/

-- Guard: refuse to finalize if any NULLs remain
IF EXISTS (SELECT 1
FROM [dbo].[User]
WHERE [MasterPasswordSalt] IS NULL)
    THROW 50000, 'MasterPasswordSalt contains NULLs; cannot finalize.', 1;

ALTER TABLE [dbo].[User] ALTER COLUMN [MasterPasswordSalt] NVARCHAR(256) NOT NULL;
GO
