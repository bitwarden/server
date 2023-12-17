-- Update "CollectionUser" with "Manage" = 1 for all users with Manager role or 'EditAssignedCollections' permission
UPDATE "CollectionUsers"
SET "ReadOnly" = 0,
    "HidePasswords" = 0,
    "Manage" = 1
WHERE "OrganizationUserId" IN (
    SELECT cu."OrganizationUserId"
    FROM "CollectionUsers" cu
             INNER JOIN "OrganizationUser" ou ON cu."OrganizationUserId" = ou."Id"
    WHERE ou."Type" = 3 OR (ou."Permissions" IS NOT NULL AND
                            JSON_VALID(ou."Permissions") AND json_extract(ou."Permissions", '$.editAssignedCollections') = 'true')
);
