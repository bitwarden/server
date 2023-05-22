CREATE VIEW [dbo].[ApiKeyDetailsView]
AS
SELECT
    AK.*,
    SA.[OrganizationId] ServiceAccountOrganizationId
FROM
    [dbo].[ApiKey] AS AK
LEFT JOIN
    [dbo].[ServiceAccount] SA ON SA.[Id] = AK.[ServiceAccountId]
