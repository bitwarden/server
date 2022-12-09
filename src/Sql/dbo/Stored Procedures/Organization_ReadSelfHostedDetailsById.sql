CREATE PROCEDURE [dbo].[Organization_ReadSelfHostedDetailsById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON
        
    EXEC [dbo].[Organization_ReadById] @Id
    EXEC [dbo].[OrganizationUser_ReadOccupySeatCountByOrganizationId] @Id
    EXEC [dbo].[Collection_ReadCountByOrganizationId] @Id
    EXEC [dbo].[Group_ReadCountByOrganizationId] @Id
    EXEC [dbo].[OrganizationUser_ReadByOrganizationId] @Id
    EXEC [dbo].[Policy_ReadByOrganizationId] @Id
    EXEC [dbo].[SsoConfig_ReadByOrganizationId] @Id
    EXEC [dbo].[OrganizationConnection_ReadByOrganizationIdType] @Id, 2 --Scim connection type
END