-- Step 1: AccessAll migration for Groups
    -- Create a temporary table to store the groups with AccessAll = 1
    CREATE TEMPORARY TABLE "TempGroupsAccessAll" AS
    SELECT "G"."Id" AS "GroupId",
           "G"."OrganizationId"
    FROM "Group" "G"
    INNER JOIN "Organization" "O" ON "G"."OrganizationId" = "O"."Id"
    WHERE "O"."FlexibleCollections" = 0 AND "G"."AccessAll" = 1;

-- Step 2: AccessAll migration for OrganizationUsers
    -- Create a temporary table to store the OrganizationUsers with AccessAll = 1
    CREATE TEMPORARY TABLE "TempUsersAccessAll" AS
    SELECT "OU"."Id" AS "OrganizationUserId",
           "OU"."OrganizationId"
    FROM "OrganizationUser" "OU"
    INNER JOIN "Organization" "O" ON "OU"."OrganizationId" = "O"."Id"
    WHERE "O"."FlexibleCollections" = 0 AND "OU"."AccessAll" = 1;

-- Step 3: For all OrganizationUsers with Manager role or 'EditAssignedCollections' permission update their existing CollectionUsers rows and insert new rows with [Manage] = 1
-- and finally update all OrganizationUsers with Manager role to User role
    -- Create a temporary table to store the OrganizationUsers with Manager role or 'EditAssignedCollections' permission
    CREATE TEMPORARY TABLE "TempUserManagers" AS
    SELECT "OU"."Id" AS "OrganizationUserId",
           "OU"."OrganizationId",
           CASE WHEN "OU"."Type" = 3 THEN 1 ELSE 0 END AS "IsManager"
    FROM "OrganizationUser" "OU"
    INNER JOIN "Organization" "O" ON "OU"."OrganizationId" = "O"."Id"
    WHERE "O"."FlexibleCollections" = 0 AND
        ("OU"."Type" = 3 OR
         ("OU"."Type" = 4 AND
          "OU"."Permissions" IS NOT NULL AND
          JSON_VALID("OU"."Permissions") AND JSON_EXTRACT(ou."Permissions", '$.editAssignedCollections') = 1));

-- Step 1
    -- Update existing rows in "CollectionGroups"
    UPDATE "CollectionGroups"
    SET
        "ReadOnly" = 0,
        "HidePasswords" = 0,
        "Manage" = 0
    WHERE EXISTS (
        SELECT 1
        FROM "Collection" "C"
        INNER JOIN "TempGroupsAccessAll" "TG" ON "CollectionGroups"."GroupId" = "TG"."GroupId"
        WHERE "CollectionGroups"."CollectionId" = "C"."Id" AND C."OrganizationId" = "TG"."OrganizationId"
    );

    -- Insert new rows into "CollectionGroups"
    INSERT INTO "CollectionGroups" ("CollectionId", "GroupId", "ReadOnly", "HidePasswords", "Manage")
    SELECT "C"."Id", "TG"."GroupId", 0, 0, 0
    FROM "Collection" "C"
    INNER JOIN "TempGroupsAccessAll" "TG" ON "C"."OrganizationId" = "TG"."OrganizationId"
    LEFT JOIN "CollectionGroups" "CG" ON "CG"."CollectionId" = "C"."Id" AND "CG"."GroupId" = "TG"."GroupId"
    WHERE "CG"."CollectionId" IS NULL;

    -- Update "Group" to clear "AccessAll" flag and update "RevisionDate"
    UPDATE "Group"
    SET "AccessAll" = 0, "RevisionDate" = CURRENT_TIMESTAMP
    WHERE "Id" IN (SELECT "GroupId" FROM "TempGroupsAccessAll");

-- Step 2
    -- Update existing rows in "CollectionUsers"
    UPDATE "CollectionUsers"
    SET "ReadOnly" = 0,
        "HidePasswords" = 0,
        "Manage" = 0
    WHERE EXISTS (
        SELECT 1
        FROM "Collection" AS "C"
        INNER JOIN "TempUsersAccessAll" AS "TU" ON "CollectionUsers"."OrganizationUserId" = "TU"."OrganizationUserId" AND
                                                   "C"."OrganizationId" = "TU"."OrganizationId"
        WHERE "CollectionUsers"."CollectionId" = "C"."Id"
    );

    -- Insert new rows into "CollectionUsers"
    INSERT INTO "CollectionUsers" ("CollectionId", "OrganizationUserId", "ReadOnly", "HidePasswords", "Manage")
    SELECT "C"."Id", "TU"."OrganizationUserId", 0, 0, 0
    FROM "Collection" "C"
    INNER JOIN "TempUsersAccessAll" "TU" ON "C"."OrganizationId" = "TU"."OrganizationId"
    LEFT JOIN "CollectionUsers" "target"
        ON "target"."CollectionId" = "C"."Id" AND "target"."OrganizationUserId" = "TU"."OrganizationUserId"
    WHERE "target"."CollectionId" IS NULL;

    -- Update "OrganizationUser" to clear "AccessAll" flag
    UPDATE "OrganizationUser"
    SET "AccessAll" = 0, "RevisionDate" = CURRENT_TIMESTAMP
    WHERE "Id" IN (SELECT "OrganizationUserId" FROM "TempUsersAccessAll");

-- Step 3
    -- Update "CollectionUsers" with "Manage" = 1 using the temporary table
    UPDATE "CollectionUsers"
    SET
        "ReadOnly" = 0,
        "HidePasswords" = 0,
        "Manage" = 1
    WHERE "OrganizationUserId" IN (SELECT "OrganizationUserId" FROM "TempUserManagers");

    -- Insert rows to "CollectionUsers" with "Manage" = 1 using the temporary table
    -- This is for orgUsers who are Managers / EditAssignedCollections but have access via a group
    -- We cannot give the whole group Manage permissions so we have to give them a direct assignment
    INSERT INTO "CollectionUsers" ("CollectionId", "OrganizationUserId", "ReadOnly", "HidePasswords", "Manage")
    SELECT DISTINCT "CG"."CollectionId", "TUM"."OrganizationUserId", 0, 0, 1
    FROM "CollectionGroups" "CG"
    INNER JOIN "GroupUser" "GU" ON "CG"."GroupId" = "GU"."GroupId"
    INNER JOIN "TempUserManagers" "TUM" ON "GU"."OrganizationUserId" = "TUM"."OrganizationUserId"
    WHERE NOT EXISTS (
        SELECT 1 FROM "CollectionUsers" "CU"
        WHERE "CU"."CollectionId" = "CG"."CollectionId" AND "CU"."OrganizationUserId" = "TUM"."OrganizationUserId"
    );

    -- Update "OrganizationUser" to migrate all OrganizationUsers with Manager role to User role
    UPDATE "OrganizationUser"
    SET "Type" = 2, "RevisionDate" = CURRENT_TIMESTAMP -- User
    WHERE "Id" IN (SELECT "OrganizationUserId" FROM "TempUserManagers" WHERE "IsManager" = 1);

-- Step 4
    -- Update "User" "AccountRevisionDate" for each unique "OrganizationUserId"
    UPDATE "User"
    SET "AccountRevisionDate" = CURRENT_TIMESTAMP
    WHERE "Id" IN (
        SELECT DISTINCT "OU"."UserId"
        FROM "OrganizationUser" "OU"
        INNER JOIN (
            -- Step 1
            SELECT "GU"."OrganizationUserId"
            FROM "GroupUser" "GU"
            INNER JOIN "TempGroupsAccessAll" "TG" ON "GU"."GroupId" = "TG"."GroupId"

            UNION

            -- Step 2
            SELECT "OrganizationUserId"
            FROM "TempUsersAccessAll"

            UNION

            -- Step 3
            SELECT "OrganizationUserId"
            FROM "TempUserManagers"
        ) AS "CombinedOrgUsers" ON "OU"."Id" = "CombinedOrgUsers"."OrganizationUserId"
    );

-- Step 5: Set "FlexibleCollections" = 1 for all organizations that have not yet been migrated.
    UPDATE "Organization"
    SET "FlexibleCollections" = 1
    WHERE "FlexibleCollections" = 0;

-- Step 6: Drop the temporary tables
    DROP TABLE IF EXISTS "TempGroupsAccessAll";
    DROP TABLE IF EXISTS "TempUsersAccessAll";
    DROP TABLE IF EXISTS "TempUserManagers";
