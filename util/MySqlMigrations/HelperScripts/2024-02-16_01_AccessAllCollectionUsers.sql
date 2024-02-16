-- Step 1: Create a temporary table
CREATE TEMPORARY TABLE TempOrgUser AS
SELECT `Id` AS `OrganizationUserId`, `OrganizationId`
FROM `OrganizationUser`
WHERE `AccessAll` = 1;

-- Step 2: Update existing rows in CollectionUsers
UPDATE `CollectionUsers` AS `target`
INNER JOIN `Collection` AS `C` ON `target`.`CollectionId` = `C`.`Id`
INNER JOIN `TempOrgUser` AS `OU` ON `C`.`OrganizationId` = `OU`.`OrganizationId`
SET
    `target`.`ReadOnly` = 0,
    `target`.`HidePasswords` = 0,
    `target`.`Manage` = 0;

-- Step 3: Insert new rows into CollectionUsers
INSERT INTO `CollectionUsers` (`CollectionId`, `OrganizationUserId`, `ReadOnly`, `HidePasswords`, `Manage`)
SELECT `C`.`Id` AS `CollectionId`, `OU`.`OrganizationUserId`, 0, 0, 0
FROM `Collection` AS `C`
INNER JOIN `TempOrgUser` AS `OU` ON `C`.`OrganizationId` = `OU`.`OrganizationId`
WHERE NOT EXISTS (
    SELECT 1
    FROM `CollectionUsers` AS `CU`
    WHERE `CU`.`CollectionId` = `C`.`Id` AND `CU`.`OrganizationUserId` = `OU`.`OrganizationUserId`
);

-- Step 4: Update OrganizationUser to clear AccessAll flag
UPDATE `OrganizationUser` AS `OU`
INNER JOIN `TempOrgUser` AS `T` ON `OU`.`Id` = `T`.`OrganizationUserId`
SET `OU`.`AccessAll` = 0;

-- Step 5: Update `User` AccountRevisionDate for each unique OrganizationUserId
UPDATE `User` AS `U`
INNER JOIN `OrganizationUser` AS `OU` ON `OU`.`UserId` = `U`.`Id`
INNER JOIN `TempOrgUser` AS `TOU` ON `TOU`.`OrganizationUserId` = `OU`.`Id`
SET `U`.`AccountRevisionDate` = UTC_TIMESTAMP()
WHERE `OU`.`Status` = 2;

-- Step 6: Drop the temporary table
DROP TEMPORARY TABLE IF EXISTS TempOrgUser;
