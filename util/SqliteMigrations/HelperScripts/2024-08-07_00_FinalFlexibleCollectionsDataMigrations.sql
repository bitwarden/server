-- Migrate Custom users who only have 'editAssignedCollections' and/or 'deleteAssignedCollections' custom permissions to the User type.
UPDATE [OrganizationUser]
SET
    [Type] = 2,
    [Permissions] = NULL
WHERE
    [Type] = 4
    AND json_valid([Permissions]) = 1
    AND (
        json_extract([Permissions], '$.editAssignedCollections') = 1
        OR json_extract([Permissions], '$.deleteAssignedCollections') = 1
    )
    AND json_extract([Permissions], '$.accessEventLogs') = 0
    AND json_extract([Permissions], '$.accessImportExport') = 0
    AND json_extract([Permissions], '$.accessReports') = 0
    AND json_extract([Permissions], '$.createNewCollections') = 0
    AND json_extract([Permissions], '$.editAnyCollection') = 0
    AND json_extract([Permissions], '$.deleteAnyCollection') = 0
    AND json_extract([Permissions], '$.manageGroups') = 0
    AND json_extract([Permissions], '$.managePolicies') = 0
    AND json_extract([Permissions], '$.manageSso') = 0
    AND json_extract([Permissions], '$.manageUsers') = 0
    AND json_extract([Permissions], '$.manageResetPassword') = 0
    AND json_extract([Permissions], '$.manageScim') = 0;

-- Remove 'editAssignedCollections' and 'deleteAssignedCollections' properties from Permissions
UPDATE [OrganizationUser]
SET
    [Permissions] = json_remove(
        json_remove([Permissions], '$.editAssignedCollections'),
        '$.deleteAssignedCollections'
    )
WHERE
    json_valid([Permissions]) = 1
    AND (
        json_extract([Permissions], '$.editAssignedCollections') IS NOT NULL
        OR json_extract([Permissions], '$.deleteAssignedCollections') IS NOT NULL
    );
