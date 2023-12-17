-- Step 1: Insert into a temporary table without batching
CREATE TEMPORARY TABLE TempOrgUser AS
SELECT Id AS OrganizationUserId, OrganizationId
FROM OrganizationUser
WHERE AccessAll = 1;

-- Step 2: Process all records at once
-- Update existing rows in CollectionUser
UPDATE CollectionUsers
SET
    ReadOnly = 0,
    HidePasswords = 0,
    Manage = 0
    FROM CollectionUsers AS target
INNER JOIN (
    SELECT C.Id AS CollectionId, T.OrganizationUserId
    FROM Collection C
    INNER JOIN TempOrgUser T ON C.OrganizationId = T.OrganizationId
) AS source
ON target.CollectionId = source.CollectionId AND target.OrganizationUserId = source.OrganizationUserId;

-- Insert new rows into CollectionUser
INSERT INTO CollectionUsers (CollectionId, OrganizationUserId, ReadOnly, HidePasswords, Manage)
SELECT source.CollectionId, source.OrganizationUserId, 0, 0, 0
FROM (
         SELECT C.Id AS CollectionId, T.OrganizationUserId
         FROM Collection C
                  INNER JOIN TempOrgUser T ON C.OrganizationId = T.OrganizationId
     ) AS source
         LEFT JOIN CollectionUsers AS target
                   ON target.CollectionId = source.CollectionId AND target.OrganizationUserId = source.OrganizationUserId
WHERE target.CollectionId IS NULL;

-- Step 3: Drop the temporary table
DROP TABLE TempOrgUser;
