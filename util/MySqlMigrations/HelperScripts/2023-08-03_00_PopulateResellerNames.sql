-- Reseller Providers were being created with a NULL value in the `Name` column.
-- This script will populate them with the value from `BusinessName` which was already required.
UPDATE `Provider`
SET `Name` = `BusinessName`
WHERE `Name` IS NULL;
