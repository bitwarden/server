-- Update `CollectionUser` with `Manage` = 1 for all users with Manager role or 'EditAssignedCollections' permission
UPDATE CollectionUsers cu
INNER JOIN OrganizationUser ou ON cu.OrganizationUserId = ou.Id
SET cu.ReadOnly = 0,
    cu.HidePasswords = 0,
    cu.Manage = 1
WHERE ou.Type = 3 OR (ou.Permissions IS NOT NULL AND
    JSON_VALID(ou.Permissions) > 0 AND JSON_UNQUOTE(JSON_EXTRACT(ou.Permissions, '$.editAssignedCollections')) = 'true');

-- Insert rows to CollectionUser for Managers and users with 'EditAssignedCollections' permission assigned to groups with collection access
INSERT INTO CollectionUsers (CollectionId, OrganizationUserId, ReadOnly, HidePasswords, Manage)
SELECT cg.CollectionId, ou.Id, 0, 0, 1
FROM CollectionGroups cg
INNER JOIN GroupUser gu ON cg.GroupId = gu.GroupId
INNER JOIN OrganizationUser ou ON gu.OrganizationUserId = ou.Id
WHERE (ou.Type = 3 OR
       (ou.Permissions IS NOT NULL AND JSON_VALID(ou.Permissions) > 0 AND JSON_UNQUOTE(JSON_EXTRACT(ou.Permissions, '$.editAssignedCollections')) = 'true'))
  AND NOT EXISTS (
    SELECT 1 FROM CollectionUsers cu
    WHERE cu.CollectionId = cg.CollectionId AND cu.OrganizationUserId = ou.Id);
