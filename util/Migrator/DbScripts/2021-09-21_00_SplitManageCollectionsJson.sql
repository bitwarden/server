-- Split Manage Assigned Collections into edit and delete
UPDATE [vault_dev].[dbo].[OrganizationUser]
SET [Permissions] = 
    JSON_MODIFY(
        JSON_MODIFY(
            JSON_MODIFY(
                [Permissions], '$.editAssignedCollections', ISNULL(JSON_VALUE([Permissions], '$.manageAssignedCollections'), 'false')
            ),
            '$.deleteAssignedCollections', ISNULL(JSON_VALUE([Permissions], '$.manageAssignedCollections'), 'false')
        ),
        '$.manageAssignedCollections', NULL
    )
WHERE [Permissions] IS NOT NULL
    AND ISJSON([Permissions]) > 0
    AND JSON_VALUE([Permissions], '$.manageAllCollections') IS NOT NULL

-- Split Manage All Collections into create, edit, and delete
UPDATE [vault_dev].[dbo].[OrganizationUser]
SET [Permissions] = 
    JSON_MODIFY(
        JSON_MODIFY(
            JSON_MODIFY(
                JSON_MODIFY(
                    [Permissions], '$.createNewCollections', ISNULL(JSON_VALUE([Permissions], '$.manageAllCollections'), 'false')
                ),
                '$.editAnyCollection', ISNULL(JSON_VALUE([Permissions], '$.manageAllCollections'), 'false')
            ),
            '$.deleteAnyCollection', ISNULL(JSON_VALUE([Permissions], '$.manageAllCollections'), 'false')
        ),
        '$.manageAllCollections', NULL
    )
WHERE [Permissions] IS NOT NULL
    AND ISJSON([Permissions]) > 0 
    AND JSON_VALUE([Permissions], '$.manageAllCollections') IS NOT NULL
