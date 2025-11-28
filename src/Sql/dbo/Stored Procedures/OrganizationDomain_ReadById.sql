CREATE PROCEDURE [dbo].[OrganizationDomain_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON
        
    SELECT 
        *
    FROM
        [dbo].[OrganizationDomain]
    WHERE
        [Id] = @Id
END