-- Split Manage Assigned Collections into edit and delete
UPDATE `bw-vault`.`OrganizationUser`
SET `Permissions` =
	JSON_REMOVE(
		JSON_INSERT(`Permissions`,
			'$.editAssignedCollections', IFNULL(JSON_EXTRACT(`Permissions`, '$.manageAssignedCollections'), 'false'),
			'$.deleteAssignedCollections', IFNULL(JSON_EXTRACT(`Permissions`, '$.manageAssignedCollections'), 'false')
        ),
        '$.manageAssignedCollections'
	)
WHERE `Permissions` IS NOT NULL
    AND JSON_VALID(`Permissions`) > 0
    AND JSON_EXTRACT(`Permissions`, '$.manageAssignedCollections') IS NOT NULL;

-- Split Manage All Collections into create, edit, and delete
UPDATE `bw-vault`.`OrganizationUser`
SET `Permissions` =
	JSON_REMOVE(
		JSON_INSERT(`Permissions`,
			'$.createNewCollections', IFNULL(JSON_EXTRACT(`Permissions`, '$.manageAllCollections'), 'false'),
			'$.editAnyCollection', IFNULL(JSON_EXTRACT(`Permissions`, '$.manageAllCollections'), 'false'),
			'$.deleteAnyCollection', IFNULL(JSON_EXTRACT(`Permissions`, '$.manageAllCollections'), 'false')
		),
		'$.manageAllCollections'
	)
WHERE `Permissions` IS NOT NULL
	AND JSON_VALID(`Permissions`) > 0
    AND JSON_EXTRACT(`Permissions`, '$.manageAllCollections') IS NOT NULL;

SELECT `Permissions` FROM `bw-vault`.`OrganizationUser`
