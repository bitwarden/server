-- Migrate Custom users who only have 'editAssignedCollections' and/or 'deleteAssignedCollections' custom permissions to the User type.
UPDATE `OrganizationUser`
SET
    `Type` = 2,
    `Permissions` = NULL
WHERE
    `Type` = 4
    AND JSON_VALID(`Permissions`) = 1
    AND (
        JSON_VALUE(`Permissions`, '$.editAssignedCollections') = 'true'
        OR JSON_VALUE(`Permissions`, '$.deleteAssignedCollections') = 'true'
    )
    AND JSON_VALUE(`Permissions`, '$.accessEventLogs') = 'false'
    AND JSON_VALUE(`Permissions`, '$.accessImportExport') = 'false'
    AND JSON_VALUE(`Permissions`, '$.accessReports') = 'false'
    AND JSON_VALUE(`Permissions`, '$.createNewCollections') = 'false'
    AND JSON_VALUE(`Permissions`, '$.editAnyCollection') = 'false'
    AND JSON_VALUE(`Permissions`, '$.deleteAnyCollection') = 'false'
    AND JSON_VALUE(`Permissions`, '$.manageGroups') = 'false'
    AND JSON_VALUE(`Permissions`, '$.managePolicies') = 'false'
    AND JSON_VALUE(`Permissions`, '$.manageSso') = 'false'
    AND JSON_VALUE(`Permissions`, '$.manageUsers') = 'false'
    AND JSON_VALUE(`Permissions`, '$.manageResetPassword') = 'false'
    AND JSON_VALUE(`Permissions`, '$.manageScim') = 'false';

-- Remove 'editAssignedCollections' and 'deleteAssignedCollections' properties from Permissions
UPDATE `OrganizationUser`
SET
    `Permissions` = JSON_REMOVE(
            JSON_REMOVE(
                `Permissions`,
                 '$.editAssignedCollections'
            ),
            '$.deleteAssignedCollections'
        )
WHERE
    JSON_VALID(`Permissions`) = 1
    AND (
        JSON_VALUE(`Permissions`, '$.editAssignedCollections') IS NOT NULL
        OR JSON_VALUE(`Permissions`, '$.deleteAssignedCollections') IS NOT NULL
    );
