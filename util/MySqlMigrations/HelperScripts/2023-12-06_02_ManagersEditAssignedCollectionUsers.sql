-- Update `CollectionUser` with `Manage` = 1 for all users with Manager role or 'EditAssignedCollections' permission
UPDATE CollectionUsers cu
    INNER JOIN OrganizationUser ou ON cu.OrganizationUserId = ou.Id
    SET cu.ReadOnly = 0,
        cu.HidePasswords = 0,
        cu.Manage = 1
WHERE ou.Type = 3 OR (ou.Permissions IS NOT NULL AND
    JSON_VALID(ou.Permissions) AND JSON_UNQUOTE(JSON_EXTRACT(ou.Permissions, '$.editAssignedCollections')) = 'true');
