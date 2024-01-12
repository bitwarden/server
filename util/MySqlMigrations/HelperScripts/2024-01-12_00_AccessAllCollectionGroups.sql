-- Step 1: Create a temporary table to store the groups with AccessAll = 1
CREATE TEMPORARY TABLE IF NOT EXISTS TempGroup AS
SELECT `Id` AS `GroupId`, `OrganizationId`
FROM `Group`
WHERE `AccessAll` = 1;

-- Step 2: Create a temporary table to store distinct OrganizationUserIds
CREATE TEMPORARY TABLE IF NOT EXISTS TempOrganizationUsers AS
SELECT DISTINCT GU.`OrganizationUserId`
FROM `GroupUser` GU
JOIN TempGroup TG ON GU.`GroupId` = TG.`GroupId`;

-- Step 3: Update existing rows in `CollectionGroups`
UPDATE `CollectionGroups` CG
INNER JOIN `Collection` C ON CG.`CollectionId` = C.`Id`
INNER JOIN TempGroup TG ON CG.`GroupId` = TG.`GroupId`
SET
    CG.`ReadOnly` = 0,
    CG.`HidePasswords` = 0,
    CG.`Manage` = 0
WHERE C.`OrganizationId` = TG.`OrganizationId`;

-- Step 4: Insert new rows into `CollectionGroups`
INSERT INTO `CollectionGroups` (`CollectionId`, `GroupId`, `ReadOnly`, `HidePasswords`, `Manage`)
SELECT C.`Id`, TG.`GroupId`, 0, 0, 0
FROM `Collection` C
INNER JOIN TempGroup TG
    ON C.`OrganizationId` = TG.`OrganizationId`
LEFT JOIN `CollectionGroups` CG
    ON CG.`CollectionId` = C.`Id` AND CG.`GroupId` = TG.`GroupId`
WHERE CG.`CollectionId` IS NULL;

-- Step 5: Update Group to clear AccessAll flag
UPDATE `Group` G
INNER JOIN TempGroup TG ON G.`Id` = TG.`GroupId`
SET G.`AccessAll` = 0;

-- Step 6: Update User AccountRevisionDate for each unique OrganizationUserId
UPDATE `User` U
INNER JOIN `OrganizationUser` OU ON OU.`UserId` = U.`Id`
INNER JOIN TempOrganizationUsers TOU ON TOU.`OrganizationUserId` = OU.`Id`
SET U.`AccountRevisionDate` = UTC_TIMESTAMP()
WHERE OU.`Status` = 2;

-- Step 7: Drop the temporary tables
DROP TEMPORARY TABLE IF EXISTS TempGroup;
DROP TEMPORARY TABLE IF EXISTS TempOrganizationUsers;
