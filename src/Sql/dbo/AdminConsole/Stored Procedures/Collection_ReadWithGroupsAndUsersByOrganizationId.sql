CREATE PROCEDURE [dbo].[Collection_ReadWithGroupsAndUsersByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON
    
    EXEC [dbo].[Collection_ReadByOrganizationId] @OrganizationId

    EXEC [dbo].[CollectionGroup_ReadByOrganizationId] @OrganizationId

    EXEC [dbo].[CollectionUser_ReadByOrganizationId] @OrganizationId
    
END