CREATE OR ALTER PROCEDURE [dbo].[OrganizationDomain_ReadByIdOrganizationId]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

SELECT
    *
FROM
    [dbo].[OrganizationDomain]
WHERE
    [Id] = @Id
  AND
    [OrganizationId] = @OrganizationId
END
GO
