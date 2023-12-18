-- Update [dbo].[CollectionUser] with [Manage] = 1 for all users with Manager role or 'EditAssignedCollections' permission
UPDATE cu
SET cu.[ReadOnly] = 0,
    cu.[HidePasswords] = 0,
    cu.[Manage] = 1
FROM [dbo].[CollectionUser] cu
INNER JOIN [dbo].[OrganizationUser] ou
    ON cu.[OrganizationUserId] = ou.[Id]
WHERE (ou.[Type] = 3 OR (ou.[Permissions] IS NOT NULL AND
    ISJSON(ou.[Permissions]) > 0 AND JSON_VALUE(ou.[Permissions], '$.editAssignedCollections') = 'true'))

-- Insert rows to [dbo].[CollectionUser] for Managers and users with 'EditAssignedCollections' permission assigned to groups with collection access
INSERT INTO [dbo].[CollectionUser] ([CollectionId], [OrganizationUserId], [ReadOnly], [HidePasswords], [Manage])
SELECT cg.[CollectionId], ou.[Id], 0, 0, 1
FROM [dbo].[CollectionGroup] cg
INNER JOIN [dbo].[GroupUser] gu
    ON cg.GroupId = gu.GroupId
INNER JOIN [dbo].[OrganizationUser] ou
    ON gu.OrganizationUserId = ou.[Id]
WHERE (ou.[Type] = 3 OR
    (ou.[Permissions] IS NOT NULL AND ISJSON(ou.[Permissions]) > 0 AND JSON_VALUE(ou.[Permissions], '$.editAssignedCollections') = 'true'))
  AND NOT EXISTS (
    SELECT 1 FROM [dbo].[CollectionUser] cu
    WHERE cu.[CollectionId] = cg.[CollectionId] AND cu.[OrganizationUserId] = ou.[Id]
  )
