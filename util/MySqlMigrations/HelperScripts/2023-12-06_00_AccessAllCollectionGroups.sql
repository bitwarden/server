-- Step 1: Create a temporary table to store the groups with AccessAll = 1
CREATE TEMPORARY TABLE IF NOT EXISTS TempGroup AS
SELECT `Id` AS `GroupId`, `OrganizationId`
FROM `Group`
WHERE `AccessAll` = 1;

-- Step 2: Update existing rows in `CollectionGroup`
UPDATE `CollectionGroups` CG
    INNER JOIN `Collection` C ON CG.`CollectionId` = C.`Id`
    INNER JOIN TempGroup TG ON CG.`GroupId` = TG.`GroupId`
    SET
        CG.`ReadOnly` = 0,
        CG.`HidePasswords` = 0,
        CG.`Manage` = 0
WHERE C.`OrganizationId` = TG.`OrganizationId`;

-- Step 3: Insert new rows into `CollectionGroup`
INSERT INTO `CollectionGroups` (`CollectionId`, `GroupId`, `ReadOnly`, `HidePasswords`, `Manage`)
SELECT C.`Id`, TG.`GroupId`, 0, 0, 0
FROM `Collection` C
         INNER JOIN TempGroup TG ON C.`OrganizationId` = TG.`OrganizationId`
         LEFT JOIN `CollectionGroups` CG ON CG.`CollectionId` = C.`Id` AND CG.`GroupId` = TG.`GroupId`
WHERE CG.`CollectionId` IS NULL;

-- Step 4: Drop the temporary table
DROP TEMPORARY TABLE IF EXISTS TempGroup;
