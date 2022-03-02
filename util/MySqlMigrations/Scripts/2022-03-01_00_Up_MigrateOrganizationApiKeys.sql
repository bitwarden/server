INSERT INTO OrganizationApiKey(OrganizationId, Type, ApiKey, RevisionDate)
SELECT Id, 0, ApiKey, RevisionDate
FROM Organization;
