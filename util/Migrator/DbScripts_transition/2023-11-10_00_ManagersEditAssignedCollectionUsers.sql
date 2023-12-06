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
