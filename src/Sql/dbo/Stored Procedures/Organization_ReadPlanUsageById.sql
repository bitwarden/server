CREATE PROCEDURE [dbo].[Organization_ReadPlanUsageById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON
        
    EXEC [dbo].[OrganizationUser_ReadByOrganizationId] @Id, NULL
    EXEC [dbo].[Collection_ReadCountByOrganizationId] @Id
    EXEC [dbo].[Group_ReadCountByOrganizationId] @Id
    EXEC [dbo].[Policy_ReadByOrganizationId] @Id
    EXEC [dbo].[SsoConfig_ReadByOrganizationId] @Id
    EXEC [dbo].[OrganizationConnection_ReadByOrganizationIdType] @Id, 2 --Scim connection type
END