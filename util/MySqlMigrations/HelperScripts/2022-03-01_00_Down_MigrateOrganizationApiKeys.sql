UPDATE Organization
INNER JOIN OrganizationApiKey ON (Organization.Id = OrganizationApiKey.OrganizationId)
SET Organization.ApiKey = OrganizationApiKey.ApiKey;
