CREATE VIEW [dbo].[ApiKeyDetailsView]
AS
SELECT
    AK.[Id],
    AK.[ServiceAccountId],
    AK.[Name],
    AK.[HashedClientSecret],
    AK.[Scope],
    AK.[EncryptedPayload],
    AK.[Key],
    AK.[ExpireAt],
    AK.[CreationDate],
    AK.[RevisionDate],
    SA.[OrganizationId] ServiceAccountOrganizationId
FROM
    [dbo].[ApiKey] AS AK
LEFT JOIN
    [dbo].[ServiceAccount] SA ON SA.[Id] = AK.[ServiceAccountId]
