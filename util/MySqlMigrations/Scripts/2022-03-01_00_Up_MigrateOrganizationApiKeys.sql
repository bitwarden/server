INSERT INTO OrganizationApiKey(Id, OrganizationId, Type, ApiKey, RevisionDate)
SELECT UUID(), Id, 0, ApiKey, RevisionDate
FROM Organization;
