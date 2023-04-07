CREATE PROCEDURE [dbo].[OrganizationDomain_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON
        
    DELETE      
    FROM
        [dbo].[OrganizationDomain]
    WHERE
        [Id] = @Id
END