CREATE PROCEDURE [dbo].[OrganizationApplication_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
    SET NOCOUNT ON;

    DELETE FROM [dbo].[OrganizationApplication]
    WHERE [Id] = @Id;
