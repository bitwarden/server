UPDATE [User]
SET [MasterPasswordSalt] = LOWER(TRIM([Email]))
WHERE [MasterPasswordSalt] IS NULL
  AND [MasterPassword] IS NOT NULL;
