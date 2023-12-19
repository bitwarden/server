-- Step 1: Create a temporary table with an additional column for batch processing, update 50 k at a time
CREATE TEMPORARY TABLE TempOrgUser AS
SELECT OrganizationUser.Id AS OrganizationUserId, OrganizationUser.OrganizationId,
       CAST(ROW_NUMBER() OVER(ORDER BY OrganizationUser.Id) / 50000 AS SIGNED) AS Batch
FROM OrganizationUser
WHERE AccessAll = 1;

-- Step 2: Get the maximum batch number
SET @MaxBatch := (SELECT MAX(Batch) FROM TempOrgUser);
SET @CurrentBatch := 0;

-- Create the stored procedure
DROP PROCEDURE IF EXISTS ProcessBatches;
CREATE PROCEDURE ProcessBatches(INOUT currentBatch INT, IN maxBatch INT)
BEGIN
    WHILE currentBatch <= maxBatch DO
        -- Update existing rows in CollectionUsers
        UPDATE CollectionUsers AS target
            INNER JOIN (
            SELECT C.Id AS CollectionId, T.OrganizationUserId
            FROM Collection C
            INNER JOIN TempOrgUser T ON C.OrganizationId = T.OrganizationId AND T.Batch = currentBatch
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
                          INNER JOIN TempOrgUser T ON C.OrganizationId = T.OrganizationId AND T.Batch = currentBatch
             ) AS source
                 LEFT JOIN CollectionUsers AS target
                           ON target.CollectionId = source.CollectionId AND target.OrganizationUserId = source.OrganizationUserId
        WHERE target.CollectionId IS NULL;

        -- Move to the next batch
        SET currentBatch := currentBatch + 1;
    END WHILE;
END;

-- Step 3: Process each batch by calling the stored procedure
CALL ProcessBatches(@CurrentBatch, @MaxBatch);

-- Step 4: Drop the temporary table
DROP TEMPORARY TABLE IF EXISTS TempOrgUser;
