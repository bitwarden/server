-- Update existing rows in CollectionUsers
UPDATE CollectionUsers AS target
INNER JOIN (
    SELECT C.Id AS CollectionId, T.OrganizationUserId
    FROM Collection C
    INNER JOIN OrganizationUser T ON C.OrganizationId = T.OrganizationId
    WHERE T.AccessAll = 1
) AS source
    ON target.CollectionId = source.CollectionId AND target.OrganizationUserId = source.OrganizationUserId
SET
    target.ReadOnly = 0,
    target.HidePasswords = 0,
    target.Manage = 0;

-- Insert new rows into CollectionUsers
INSERT INTO CollectionUsers (CollectionId, OrganizationUserId, ReadOnly, HidePasswords, Manage)
SELECT source.CollectionId, source.OrganizationUserId, 0, 0, 0
FROM (
         SELECT C.Id AS CollectionId, T.OrganizationUserId
         FROM Collection C
                  INNER JOIN OrganizationUser T ON C.OrganizationId = T.OrganizationId
         WHERE T.AccessAll = 1
     ) AS source
LEFT JOIN CollectionUsers AS target
    ON target.CollectionId = source.CollectionId AND target.OrganizationUserId = source.OrganizationUserId
WHERE target.CollectionId IS NULL;
