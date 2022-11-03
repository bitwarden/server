CREATE OR ALTER VIEW [dbo].[ApiKeyDetailsView]
AS
SELECT
    AK.*,
    SA.[OrganizationId] ServiceAccountOrganizationId
FROM
    [dbo].[ApiKey] AS AK
LEFT JOIN
    [dbo].[ServiceAccount] SA ON SA.[Id] = AK.[ServiceAccountId]
GO

CREATE OR ALTER PROCEDURE [dbo].[ApiKeyDetails_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[ApiKeyDetailsView]
    WHERE
        [Id] = @Id
END
