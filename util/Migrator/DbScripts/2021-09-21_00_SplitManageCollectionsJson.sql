-- Split Manage Assigned Collections into edit and delete
UPDATE [dbo].[OrganizationUser]
SET [Permissions] = 
    JSON_MODIFY(
        JSON_MODIFY(
            [Permissions],
            '$.editAssignedCollections',
            CAST(ISNULL(
                    ISNULL(
                        JSON_VALUE([Permissions], '$.editAssignedCollections'),
                        JSON_VALUE([Permissions], '$.manageAssignedCollections')
                  ),
                0) AS BIT)
        ),
        '$.deleteAssignedCollections',
        CAST(ISNULL(
            ISNULL(
                JSON_VALUE([Permissions], '$.deleteAssignedCollections'),
                JSON_VALUE([Permissions], '$.manageAssignedCollections')),
            0) AS BIT)
    )
WHERE [Permissions] IS NOT NULL
    AND ISJSON([Permissions]) > 0
    AND (
        JSON_VALUE([Permissions], '$.editAssignedCollections') IS NULL
        OR JSON_VALUE([Permissions], '$.deleteAssignedCollections') IS NULL
    )

-- Split Manage All Collections into create, edit, and delete
UPDATE [dbo].[OrganizationUser]
SET [Permissions] = 
    JSON_MODIFY(
        JSON_MODIFY(
            JSON_MODIFY(
                [Permissions],
                '$.createNewCollections',
                CAST(ISNULL(
                    ISNULL(
                        JSON_VALUE([Permissions], '$.createNewCollections'),
                        JSON_VALUE([Permissions], '$.manageAllCollections')),
                    0) AS BIT)
            ),
            '$.editAnyCollection',
            CAST(ISNULL(
                ISNULL(
                    JSON_VALUE([Permissions], '$.editAnyCollection'),
                    JSON_VALUE([Permissions], '$.manageAllCollections')),
                0) AS BIT)
        ),
        '$.deleteAnyCollection',
            CAST(ISNULL(
                ISNULL(
                    JSON_VALUE([Permissions], '$.deleteAnyCollection'),
                    JSON_VALUE([Permissions], '$.manageAllCollections')),
                0) AS BIT)
    )
WHERE [Permissions] IS NOT NULL
    AND ISJSON([Permissions]) > 0 
    AND (
        JSON_VALUE([Permissions], '$.createNewCollections') IS NULL
        OR JSON_VALUE([Permissions], '$.editAnyCollection') IS NULL
        OR JSON_VALUE([Permissions], '$.deleteAnyCollection') IS NULL
    )

