/**
 * The dbo.OrganizationIntegrationConfiguration table has a "Template" column, which contains JSON
 * The format of this template has changed, and therefore,
 * the rows that contain the invalid format (i.e. "service":"Datadog" or "service":"Crowdstrike")
 * must be dropped.
 * Without dropping these records, new records cannot be created due to unique constraints on certain fields.
 * Drop obsolete integration configurations for Datadog and Crowdstrike integrations.
 */
DELETE FROM dbo.OrganizationIntegrationConfiguration WHERE Template like '%"service":"Datadog"%'
DELETE FROM dbo.OrganizationIntegrationConfiguration WHERE Template like '%"service":"Crowdstrike"%'

DELETE FROM dbo.OrganizationIntegrationConfiguration WHERE OrganizationIntegrationId in 
  (SELECT Id FROM dbo.OrganizationIntegration WHERE Type in (6) and Configuration like '%"service":"Datadog"%')
DELETE FROM dbo.OrganizationIntegrationConfiguration WHERE OrganizationIntegrationId in 
  (SELECT Id FROM dbo.OrganizationIntegration WHERE Type in (5) and Configuration like '%"service":"Crowdstrike"%')

DELETE FROM dbo.OrganizationIntegration WHERE Type in (6) and Configuration like '%"service":"Datadog"%'
DELETE FROM dbo.OrganizationIntegration WHERE Type in (5) and Configuration like '%"service":"Crowdstrike"%'
