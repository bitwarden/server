CREATE PROCEDURE [dbo].[OrganizationDomain_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON
        
    SELECT 
        *
    FROM
        [dbo].[OrganizationDomain]
    WHERE
        [OrganizationId] = @OrganizationId
END