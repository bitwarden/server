-- Step 1: Update `CollectionUser` with `Manage` = 1 for all users with Manager role or 'EditAssignedCollections' permission
    -- Create a temporary table
    CREATE TEMPORARY TABLE TempOrgUser AS
    SELECT ou.Id AS OrganizationUserId
    FROM OrganizationUser ou
    WHERE ou.Type = 3 OR (ou.Permissions IS NOT NULL AND
        JSON_VALID(ou.Permissions) > 0 AND JSON_UNQUOTE(JSON_EXTRACT(ou.Permissions, '$.editAssignedCollections')) = 'true');

    -- Update CollectionUsers with Manage = 1 using the temporary table
    UPDATE CollectionUsers cu
    INNER JOIN TempOrgUser temp ON cu.OrganizationUserId = temp.OrganizationUserId
    SET cu.ReadOnly = 0,
        cu.HidePasswords = 0,
        cu.Manage = 1;

    -- Update `User` AccountRevisionDate for each unique OrganizationUserId
    UPDATE `User` AS `U`
    INNER JOIN `OrganizationUser` AS `OU` ON `OU`.`UserId` = `U`.`Id`
    INNER JOIN `TempOrgUser` AS `TOU` ON `TOU`.`OrganizationUserId` = `OU`.`Id`
    SET `U`.`AccountRevisionDate` = UTC_TIMESTAMP()
    WHERE `OU`.`Status` = 2;

    -- Drop the temporary table
    DROP TEMPORARY TABLE IF EXISTS TempOrgUser;

-- Step 2: Insert rows to CollectionUser for Managers and users with 'EditAssignedCollections' permission assigned to groups with collection access
    -- Store the results in a temporary table
    CREATE TEMPORARY TABLE IF NOT EXISTS TempCol AS
    SELECT cg.CollectionId, ou.Id AS OrganizationUserId
    FROM CollectionGroups cg
    INNER JOIN GroupUser gu ON cg.GroupId = gu.GroupId
    INNER JOIN OrganizationUser ou ON gu.OrganizationUserId = ou.Id
    WHERE (ou.Type = 3 OR (ou.Permissions IS NOT NULL AND JSON_VALID(ou.Permissions) > 0 AND JSON_UNQUOTE(JSON_EXTRACT(ou.Permissions, '$.editAssignedCollections')) = 'true'))
      AND NOT EXISTS (
        SELECT 1 FROM CollectionUsers cu
        WHERE cu.CollectionId = cg.CollectionId AND cu.OrganizationUserId = ou.Id
    );

    -- Insert rows into CollectionUsers using the temporary table
    INSERT INTO CollectionUsers (CollectionId, OrganizationUserId, ReadOnly, HidePasswords, Manage)
    SELECT CollectionId, OrganizationUserId, 0, 0, 1
    FROM TempCol;

    -- Update `User` AccountRevisionDate for each unique OrganizationUserId
    UPDATE `User` AS `U`
    INNER JOIN `OrganizationUser` AS `OU` ON `OU`.`UserId` = `U`.`Id`
    INNER JOIN `TempCol` AS `TC` ON `TC`.`OrganizationUserId` = `OU`.`Id`
    SET `U`.`AccountRevisionDate` = UTC_TIMESTAMP()
    WHERE `OU`.`Status` = 2;

    -- Drop the temporary table
    DROP TEMPORARY TABLE IF EXISTS TempCol;

-- Step 3: Set all Managers to Users
    -- Create a temporary table
    CREATE TEMPORARY TABLE TempOrgUser AS
    SELECT ou.Id AS OrganizationUserId
    FROM OrganizationUser ou
    WHERE ou.Type = 3;

    -- Update OrganizationUser with Type = 2 using the temporary table
    UPDATE OrganizationUser ou
    INNER JOIN TempOrgUser temp ON ou.Id = temp.OrganizationUserId
    SET ou.Type = 2;

    -- Update `User` AccountRevisionDate for each unique OrganizationUserId
    UPDATE `User` AS `U`
    INNER JOIN `OrganizationUser` AS `OU` ON `OU`.`UserId` = `U`.`Id`
    INNER JOIN `TempOrgUser` AS `TOU` ON `TOU`.`OrganizationUserId` = `OU`.`Id`
    SET `U`.`AccountRevisionDate` = UTC_TIMESTAMP()
    WHERE `OU`.`Status` = 2;

    -- Drop the temporary table
    DROP TEMPORARY TABLE IF EXISTS TempOrgUser;
