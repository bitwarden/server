-- Step 1: Create a temporary table
CREATE TEMPORARY TABLE IF NOT EXISTS TempOrgUser AS
SELECT "Id" AS "OrganizationUserId", "OrganizationId"
FROM "OrganizationUser"
WHERE "AccessAll" = 1;

-- Step 2: Update existing rows in CollectionUsers
UPDATE "CollectionUsers"
SET
    "ReadOnly" = 0,
    "HidePasswords" = 0,
    "Manage" = 0
WHERE "CollectionId" IN (
    SELECT "C"."Id"
    FROM "Collection" "C"
    INNER JOIN TempOrgUser "OU" ON "C"."OrganizationId" = "OU"."OrganizationId"
);

-- Step 3: Insert new rows into CollectionUsers
INSERT INTO "CollectionUsers" ("CollectionId", "OrganizationUserId", "ReadOnly", "HidePasswords", "Manage")
SELECT C."Id" AS "CollectionId", OU."OrganizationUserId", 0, 0, 0
FROM "Collection" C
JOIN TempOrgUser OU ON C."OrganizationId" = OU."OrganizationId"
WHERE NOT EXISTS (
    SELECT 1
    FROM "CollectionUsers" "CU"
    WHERE "CU"."CollectionId" = "C"."Id" AND "CU"."OrganizationUserId" = "OU"."OrganizationUserId"
);

-- Step 4: Update OrganizationUser to clear AccessAll flag
UPDATE "OrganizationUser"
SET "AccessAll" = 0
WHERE "Id" IN (SELECT "OrganizationUserId" FROM TempOrgUser);

-- Step 5: Update "User" AccountRevisionDate for each unique OrganizationUserId
UPDATE "User"
SET "AccountRevisionDate" = CURRENT_TIMESTAMP
WHERE "Id" IN (
    SELECT OU."UserId"
    FROM "OrganizationUser" OU
    JOIN TempOrgUser TOU ON OU."Id" = TOU."OrganizationUserId"
    WHERE OU."Status" = 2
);

-- Step 6: Drop the temporary table
DROP TABLE IF EXISTS TempOrgUser;
