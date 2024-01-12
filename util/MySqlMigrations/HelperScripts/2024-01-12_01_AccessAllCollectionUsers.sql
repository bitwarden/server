-- Update existing rows in CollectionUsers
UPDATE CollectionUsers AS target
INNER JOIN Collection AS C ON target.CollectionId = C.Id
INNER JOIN OrganizationUser AS OU ON C.OrganizationId = OU.OrganizationId
SET
    target.ReadOnly = 0,
    target.HidePasswords = 0,
    target.Manage = 0
WHERE OU.AccessAll = 1;

-- Insert new rows into CollectionUsers
INSERT INTO CollectionUsers (CollectionId, OrganizationUserId, ReadOnly, HidePasswords, Manage)
SELECT C.Id AS CollectionId, OU.Id AS OrganizationUserId, 0, 0, 0
FROM Collection AS C
INNER JOIN OrganizationUser AS OU ON C.OrganizationId = OU.OrganizationId
WHERE OU.AccessAll = 1
  AND NOT EXISTS (
    SELECT 1
    FROM CollectionUsers AS CU
    WHERE CU.CollectionId = C.Id AND CU.OrganizationUserId = OU.Id
);
