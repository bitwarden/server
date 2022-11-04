CREATE PROCEDURE [dbo].[OrganizationDomain_Deactivate]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON
        
    UPDATE
        [dbo].[OrganizationDomain]
    SET
        [Active] = 0 -- False
    WHERE
        [Id] = @Id
END