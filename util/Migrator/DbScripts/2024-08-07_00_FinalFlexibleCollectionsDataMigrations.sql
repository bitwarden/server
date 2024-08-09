DECLARE @BatchSize INT = 1000;
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
WHILE 1 = 1
BEGIN
    UPDATE TOP (@BatchSize) [dbo].[OrganizationUser]
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
        WHERE
            ISJSON([Permissions]) = 1
            AND EXISTS (
                SELECT 1
                FROM OPENJSON([Permissions])
                WITH (
                    editAssignedCollections bit '$.editAssignedCollections',
                    deleteAssignedCollections bit '$.deleteAssignedCollections'
                ) AS PermissionsJson
                WHERE
                    PermissionsJson.editAssignedCollections IS NOT NULL
                    OR PermissionsJson.deleteAssignedCollections IS NOT NULL
            );

    SET @RowsAffected = @@ROWCOUNT;

    IF @RowsAffected = 0
        BREAK;
END
