-- Create a temporary table to store the groups with AccessAll = 1
CREATE TEMPORARY TABLE TempGroup AS
SELECT "Id" AS "GroupId", "OrganizationId"
FROM "Group"
WHERE "AccessAll" = 1;

-- Update existing rows in "CollectionGroup"
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

-- Insert new rows into "CollectionGroup"
INSERT INTO "CollectionGroups" ("CollectionId", "GroupId", "ReadOnly", "HidePasswords", "Manage")
SELECT C."Id", TG."GroupId", 0, 0, 0
FROM "Collection" C
         INNER JOIN TempGroup TG ON C."OrganizationId" = TG."OrganizationId"
         LEFT JOIN "CollectionGroups" CG ON CG."CollectionId" = C."Id" AND CG."GroupId" = TG."GroupId"
WHERE CG."CollectionId" IS NULL;

-- Drop the temporary table
DROP TABLE IF EXISTS TempGroup;
