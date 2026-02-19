/*
    Script to finalize the addition of MasterPasswordSalt to User table by making it NOT NULL.
    This script should be run only after all existing rows have been backfilled with non-NULL values from
    the transition script 2026-02-03_00_HydrateMasterPasswordSaltColumn.sql.
*/

-- Guard: refuse to finalize if any NULLs remain
IF EXISTS (SELECT 1
FROM [dbo].[User]
WHERE
    [MasterPassword] IS NOT NULL AND
    [MasterPasswordSalt] IS NULL)
    THROW 50000, 'MasterPasswordSalt contains NULLs for MasterPassword users; cannot finalize.', 1;

ALTER TABLE [dbo].[User] ADD CONSTRAINT [CK_User_MasterPasswordAndSalt_Required] CHECK (([MasterPassword] IS NULL AND [MasterPasswordSalt] IS NULL) OR ([MasterPassword] IS NOT NULL AND [MasterPasswordSalt] IS NOT NULL));
GO
