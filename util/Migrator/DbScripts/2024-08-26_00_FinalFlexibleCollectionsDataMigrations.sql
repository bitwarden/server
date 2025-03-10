DECLARE @BatchSize INT = 2000;
DECLARE @RowsAffected INT;

-- Migrate Custom users who only have 'editAssignedCollections' and/or 'deleteAssignedCollections'
-- custom permissions to the User type.
WHILE 1 = 1
BEGIN
    UPDATE TOP (@BatchSize) [dbo].[OrganizationUser]
    SET
        [Type] = 2,
        [Permissions] = NULL
    WHERE
        [Type] = 4
        AND ISJSON([Permissions]) = 1
        AND EXISTS (
            SELECT 1
            FROM OPENJSON([Permissions])
            WITH (
                editAssignedCollections bit '$.editAssignedCollections',
                deleteAssignedCollections bit '$.deleteAssignedCollections',
                accessEventLogs bit '$.accessEventLogs',
                accessImportExport bit '$.accessImportExport',
                accessReports bit '$.accessReports',
                createNewCollections bit '$.createNewCollections',
                editAnyCollection bit '$.editAnyCollection',
                deleteAnyCollection bit '$.deleteAnyCollection',
                manageGroups bit '$.manageGroups',
                managePolicies bit '$.managePolicies',
                manageSso bit '$.manageSso',
                manageUsers bit '$.manageUsers',
                manageResetPassword bit '$.manageResetPassword',
                manageScim bit '$.manageScim'
            ) AS PermissionsJson
            WHERE
                (PermissionsJson.editAssignedCollections = 1 OR PermissionsJson.deleteAssignedCollections = 1)
                AND PermissionsJson.accessEventLogs = 0
                AND PermissionsJson.accessImportExport = 0
                AND PermissionsJson.accessReports = 0
                AND PermissionsJson.createNewCollections = 0
                AND PermissionsJson.editAnyCollection = 0
                AND PermissionsJson.deleteAnyCollection = 0
                AND PermissionsJson.manageGroups = 0
                AND PermissionsJson.managePolicies = 0
                AND PermissionsJson.manageSso = 0
                AND PermissionsJson.manageUsers = 0
                AND PermissionsJson.manageResetPassword = 0
                AND PermissionsJson.manageScim = 0
        );

    SET @RowsAffected = @@ROWCOUNT;

    IF @RowsAffected = 0
        BREAK;
END

-- Remove 'editAssignedCollections' and 'deleteAssignedCollections' properties from Permissions
-- Step 1: Create a temporary table to store the IDs and parsed JSON values
CREATE TABLE #TempIds (
    TempId INT IDENTITY(1,1) PRIMARY KEY,
    OrganizationUserId UNIQUEIDENTIFIER,
    editAssignedCollections BIT,
    deleteAssignedCollections BIT
);

-- Step 2: Populate the temporary table with the IDs and parsed JSON values
INSERT INTO #TempIds (OrganizationUserId, editAssignedCollections, deleteAssignedCollections)
SELECT
    Id,
    CAST(JSON_VALUE([Permissions], '$.editAssignedCollections') AS BIT) AS editAssignedCollections,
    CAST(JSON_VALUE([Permissions], '$.deleteAssignedCollections') AS BIT) AS deleteAssignedCollections
FROM [dbo].[OrganizationUser]
WHERE
    ISJSON([Permissions]) = 1
    AND (
        JSON_VALUE([Permissions], '$.editAssignedCollections') IS NOT NULL
        OR JSON_VALUE([Permissions], '$.deleteAssignedCollections') IS NOT NULL
    );

DECLARE @MaxTempId INT;
DECLARE @CurrentBatchStart INT = 1;

-- Get the maximum TempId
SELECT @MaxTempId = MAX(TempId) FROM #TempIds;

-- Step 3: Loop through the IDs in batches
WHILE @CurrentBatchStart <= @MaxTempId
BEGIN
    UPDATE tu
    SET
        [Permissions] =
            JSON_MODIFY(
                JSON_MODIFY(
                    [Permissions],
                    '$.editAssignedCollections',
                    NULL
                ),
                '$.deleteAssignedCollections',
                NULL
            )
    FROM [dbo].[OrganizationUser] tu
    INNER JOIN #TempIds ti ON tu.Id = ti.OrganizationUserId
    WHERE
        ti.TempId BETWEEN @CurrentBatchStart AND @CurrentBatchStart + @BatchSize - 1
        AND (
            ti.editAssignedCollections IS NOT NULL
            OR ti.deleteAssignedCollections IS NOT NULL
        );

    SET @RowsAffected = @@ROWCOUNT;

    IF @RowsAffected = 0
        BREAK;

    SET @CurrentBatchStart = @CurrentBatchStart + @BatchSize;
END

-- Clean up the temporary table
DROP TABLE #TempIds;
