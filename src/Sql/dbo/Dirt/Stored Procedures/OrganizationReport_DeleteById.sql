CREATE PROCEDURE [dbo].[OrganizationReport_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
    SET NOCOUNT ON;

    DELETE FROM [dbo].[OrganizationReport]
    WHERE [Id] = @Id
