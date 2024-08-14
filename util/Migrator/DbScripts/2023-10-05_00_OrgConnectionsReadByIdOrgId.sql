CREATE OR ALTER PROCEDURE [dbo].[OrganizationConnection_ReadByIdOrganizationId]
    @Id UNIQUEIDENTIFIER,
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

SELECT
    *
FROM
    [dbo].[OrganizationConnectionView]
WHERE
    [Id] = @Id AND
    [OrganizationId] = @OrganizationId
END
GO
