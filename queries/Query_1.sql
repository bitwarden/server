update dbo.Organization set UseRiskInsights = 1 WHERE Id = '58ea0abf-544c-40cf-9e24-b38d00d8fb76'

SELECT * FROM dbo.Organization WHERE Id = '58ea0abf-544c-40cf-9e24-b38d00d8fb76';

DELETE FROM vault_dev.dbo.SecurityTask
where OrganizationId is not null

DELETE FROM vault_dev.dbo.OrganizationReport
where OrganizationId is not null