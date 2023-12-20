-- Update `CollectionUser` with `Manage` = 1 for all users with Manager role or 'EditAssignedCollections' permission
UPDATE "CollectionUsers" AS cu
SET
    "ReadOnly" = 0,
    "HidePasswords" = 0,
    "Manage" = 1
WHERE "OrganizationUserId" IN (
    SELECT ou."Id"
    FROM "OrganizationUser" AS ou
    WHERE ou."Type" = 3 OR (
        ou."Permissions" IS NOT NULL AND
        JSON_VALID(ou."Permissions") > 0 AND
        JSON_EXTRACT(ou."Permissions", '$.editAssignedCollections') = 'true'
        )
);

-- Insert rows to CollectionUser for Managers and users with 'EditAssignedCollections' permission assigned to groups with collection access
INSERT INTO "CollectionUsers" ("CollectionId", "OrganizationUserId", "ReadOnly", "HidePasswords", "Manage")
SELECT cg."CollectionId", ou."Id", 0, 0, 1
FROM "CollectionGroups" AS cg
         INNER JOIN "GroupUser" AS gu ON cg."GroupId" = gu."GroupId"
         INNER JOIN "OrganizationUser" AS ou ON gu."OrganizationUserId" = ou."Id"
WHERE (ou."Type" = 3 OR
       (ou."Permissions" IS NOT NULL AND JSON_VALID(ou."Permissions") > 0 AND JSON_EXTRACT(ou."Permissions", '$.editAssignedCollections')) = 'true')
  AND NOT EXISTS (
    SELECT 1 FROM "CollectionUsers" AS cu
    WHERE cu."CollectionId" = cg."CollectionId" AND cu."OrganizationUserId" = ou."Id"
);
