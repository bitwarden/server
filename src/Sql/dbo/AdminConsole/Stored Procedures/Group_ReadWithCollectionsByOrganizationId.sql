CREATE PROCEDURE [dbo].[Group_ReadWithCollectionsByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[Group_ReadByOrganizationId] @OrganizationId
        
    EXEC [dbo].[CollectionGroup_ReadByOrganizationId] @OrganizationId
END