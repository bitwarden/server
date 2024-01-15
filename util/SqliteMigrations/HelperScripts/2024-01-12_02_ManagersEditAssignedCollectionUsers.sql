-- Step 1: Update `CollectionUser` with `Manage` = 1 for all users with Manager role or 'EditAssignedCollections' permission
    -- Create a temporary table
    CREATE TEMPORARY TABLE IF NOT EXISTS TempOrgUser AS
    SELECT ou."Id" AS "OrganizationUserId"
    FROM "OrganizationUser" ou
    WHERE ou."Type" = 3 OR (
        ou."Permissions" IS NOT NULL AND
        JSON_VALID(ou."Permissions") > 0 AND
        JSON_EXTRACT(ou."Permissions", '$.editAssignedCollections') = 'true'
        );

    -- Update CollectionUsers with Manage = 1 using the temporary table
    UPDATE "CollectionUsers"
    SET
        "ReadOnly" = 0,
        "HidePasswords" = 0,
        "Manage" = 1
    WHERE "OrganizationUserId" IN (SELECT "OrganizationUserId" FROM TempOrgUser);

    -- Update `User` AccountRevisionDate for each unique OrganizationUserId
    UPDATE "User"
    SET "AccountRevisionDate" = CURRENT_TIMESTAMP
    WHERE "Id" IN (
        SELECT OU."UserId"
        FROM "OrganizationUser" OU
        JOIN TempOrgUser TOU ON OU."Id" = TOU."OrganizationUserId"
        WHERE OU."Status" = 2
    );

    -- Drop the temporary table
    DROP TABLE IF EXISTS TempOrgUser;

-- Step 2: Insert rows to CollectionUser for Managers and users with 'EditAssignedCollections' permission assigned to groups with collection access
    -- Store the results in a temporary table
    CREATE TEMPORARY TABLE IF NOT EXISTS TempCol AS
    SELECT cg."CollectionId", ou."Id" AS "OrganizationUserId"
    FROM "CollectionGroups" cg
    JOIN "GroupUser" gu ON cg."GroupId" = gu."GroupId"
    JOIN "OrganizationUser" ou ON gu."OrganizationUserId" = ou."Id"
    WHERE (ou."Type" = 3 OR (
        ou."Permissions" IS NOT NULL AND
        JSON_VALID(ou."Permissions") > 0 AND
        JSON_EXTRACT(ou."Permissions", '$.editAssignedCollections') = 'true'
        ))
      AND NOT EXISTS (
        SELECT 1 FROM "CollectionUsers" cu
        WHERE cu."CollectionId" = cg."CollectionId" AND cu."OrganizationUserId" = ou."Id"
    );

    -- Insert rows into CollectionUsers using the temporary table
    INSERT INTO "CollectionUsers" ("CollectionId", "OrganizationUserId", "ReadOnly", "HidePasswords", "Manage")
    SELECT "CollectionId", "OrganizationUserId", 0, 0, 1
    FROM TempCol;

    -- Update `User` AccountRevisionDate for each unique OrganizationUserId
    UPDATE "User"
    SET "AccountRevisionDate" = CURRENT_TIMESTAMP
    WHERE "Id" IN (
        SELECT OU."UserId"
        FROM "OrganizationUser" OU
        JOIN TempCol TC ON TC."OrganizationUserId" = OU."Id"
        WHERE OU."Status" = 2
    );

    -- Drop the temporary table
    DROP TABLE IF EXISTS TempCol;

-- Step 3: Set all Managers to Users
    -- Create a temporary table
    CREATE TEMPORARY TABLE IF NOT EXISTS TempOrgUser AS
    SELECT ou."Id" AS "OrganizationUserId"
    FROM "OrganizationUser" ou
    WHERE ou."Type" = 3;

    -- Update OrganizationUser with Type = 2 using the temporary table
    UPDATE "OrganizationUser"
    SET "Type" = 2
    WHERE "Id" IN (SELECT "OrganizationUserId" FROM TempOrgUser);

    -- Update `User` AccountRevisionDate for each unique OrganizationUserId
    UPDATE "User"
    SET "AccountRevisionDate" = CURRENT_TIMESTAMP
    WHERE "Id" IN (
        SELECT OU."UserId"
        FROM "OrganizationUser" OU
        JOIN TempOrgUser TOU ON TOU."OrganizationUserId" = OU."Id"
        WHERE OU."Status" = 2
    );

    -- Drop the temporary table
    DROP TABLE IF EXISTS TempOrgUser;
