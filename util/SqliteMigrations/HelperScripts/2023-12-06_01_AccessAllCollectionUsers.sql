-- Update existing rows in CollectionUsers
UPDATE "CollectionUsers"
SET
    "ReadOnly" = 0,
    "HidePasswords" = 0,
    "Manage" = 0
WHERE "CollectionId" IN (
    SELECT "C"."Id"
    FROM "Collection" "C"
             INNER JOIN "OrganizationUser" "OU" ON "C"."OrganizationId" = "OU"."OrganizationId"
    WHERE "OU"."AccessAll" = 1
);

-- Insert new rows into CollectionUsers
INSERT INTO "CollectionUsers" ("CollectionId", "OrganizationUserId", "ReadOnly", "HidePasswords", "Manage")
SELECT "C"."Id" AS "CollectionId", "OU"."Id" AS "OrganizationUserId", 0, 0, 0
FROM "Collection" "C"
         INNER JOIN "OrganizationUser" "OU" ON "C"."OrganizationId" = "OU"."OrganizationId"
WHERE "OU"."AccessAll" = 1
  AND NOT EXISTS (
    SELECT 1
    FROM "CollectionUsers" "CU"
    WHERE "CU"."CollectionId" = "C"."Id" AND "CU"."OrganizationUserId" = "OU"."Id"
);
