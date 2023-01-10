-- Split Manage Assigned Collections into edit and delete
UPDATE `OrganizationUser`
SET `Permissions` =
	JSON_INSERT(
		`Permissions`,
		'$.editAssignedCollections',
		IFNULL(
			IFNULL(
				JSON_EXTRACT(`Permissions`,'$.editAssignedCollections'),
				JSON_EXTRACT(`Permissions`, '$.manageAssignedCollections')),
			false),
		'$.deleteAssignedCollections', 
		IFNULL(
			IFNULL(
				JSON_EXTRACT(`Permissions`, '$.deleteAssignedCollections'),
				JSON_EXTRACT(`Permissions`, '$.manageAssignedCollections')),
			false)
	)
WHERE `Permissions` IS NOT NULL
    AND JSON_VALID(`Permissions`) > 0
    AND (
		JSON_EXTRACT(`Permissions`, '$.editAssignedCollections') IS NULL
		OR JSON_EXTRACT(`Permissions`, '$.deleteAssignedCollections') IS NULL
	);

-- Split Manage All Collections into create, edit, and delete
UPDATE `OrganizationUser`
SET `Permissions` =
	JSON_INSERT(
		`Permissions`,
		'$.createNewCollections',
		IFNULL(
			IFNULL(
				JSON_EXTRACT(`Permissions`, '$.createNewColletions'),
				JSON_EXTRACT(`Permissions`, '$.manageAllCollections')),
			false),
		'$.editAnyCollection',
		IFNULL(
			IFNULL(
				JSON_EXTRACT(`Permissions`, '$.editAnyCollection'),
				JSON_EXTRACT(`Permissions`, '$.manageAllCollections')),
			false),
		'$.deleteAnyCollection',
		IFNULL(
			IFNULL(
				JSON_EXTRACT(`Permissions`, '$.deleteAnyCollection'),
				JSON_EXTRACT(`Permissions`, '$.manageAllCollections')),
			false)
	)
WHERE `Permissions` IS NOT NULL
	AND JSON_VALID(`Permissions`) > 0
    AND (
		JSON_EXTRACT(`Permissions`, '$.createNewCollections') IS NULL
		OR JSON_EXTRACT(`Permissions`, '$.editAnyCollection') IS NULL
		OR JSON_EXTRACT(`Permissions`, '$.deleteAnyCollection') IS NULL
	);
