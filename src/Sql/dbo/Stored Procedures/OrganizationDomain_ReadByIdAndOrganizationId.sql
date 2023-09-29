CREATE PROCEDURE [dbo].[OrganizationDomain_ReadByIdAndOrganizationId]
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
