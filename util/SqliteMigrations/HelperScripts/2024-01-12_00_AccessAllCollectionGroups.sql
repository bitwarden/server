-- Step 1: Create a temporary table to store the groups with AccessAll = 1
CREATE TEMPORARY TABLE IF NOT EXISTS TempGroup AS
SELECT "Id" AS "GroupId", "OrganizationId"
FROM "Group"
WHERE "AccessAll" = 1;

-- Step 2: Create a temporary table to store distinct OrganizationUserIds
CREATE TEMPORARY TABLE IF NOT EXISTS TempOrganizationUsers AS
SELECT DISTINCT GU."OrganizationUserId"
FROM "GroupUser" GU
JOIN TempGroup TG ON GU."GroupId" = TG."GroupId";

-- Step 3: Update existing rows in "CollectionGroups"
UPDATE "CollectionGroups"
SET
    "ReadOnly" = 0,
    "HidePasswords" = 0,
    "Manage" = 0
WHERE EXISTS (
    SELECT 1
    FROM "Collection" C
    INNER JOIN TempGroup TG ON "CollectionGroups"."GroupId" = TG."GroupId"
    WHERE "CollectionGroups"."CollectionId" = C."Id" AND C."OrganizationId" = TG."OrganizationId"
);

-- Step 4: Insert new rows into "CollectionGroups"
INSERT INTO "CollectionGroups" ("CollectionId", "GroupId", "ReadOnly", "HidePasswords", "Manage")
SELECT C."Id", TG."GroupId", 0, 0, 0
FROM "Collection" C
INNER JOIN TempGroup TG ON C."OrganizationId" = TG."OrganizationId"
LEFT JOIN "CollectionGroups" CG ON C."Id" = CG."CollectionId" AND TG."GroupId" = CG."GroupId"
WHERE CG."CollectionId" IS NULL;

-- Step 5: Update Group to clear AccessAll flag
UPDATE "Group"
SET "AccessAll" = 0, "RevisionDate" = CURRENT_TIMESTAMP
WHERE "Id" IN (SELECT "GroupId" FROM TempGroup);

-- Step 6: Update User AccountRevisionDate for each unique OrganizationUserId
UPDATE "User"
SET "AccountRevisionDate" = CURRENT_TIMESTAMP
WHERE "Id" IN (
    SELECT OU."UserId"
    FROM "OrganizationUser" OU
    JOIN TempOrganizationUsers TOU ON OU."Id" = TOU."OrganizationUserId"
    WHERE OU."Status" = 2
);

-- Step 7: Drop the temporary tables
DROP TABLE IF EXISTS TempGroup;
DROP TABLE IF EXISTS TempOrganizationUsers;
