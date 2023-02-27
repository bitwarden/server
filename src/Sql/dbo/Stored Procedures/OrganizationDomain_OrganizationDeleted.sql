CREATE PROCEDURE [dbo].[OrganizationDomain_OrganizationDeleted]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON
        
    DELETE 
    FROM
        [dbo].[OrganizationDomain]
    WHERE
        [OrganizationId] = @OrganizationId
END