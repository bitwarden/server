CREATE PROCEDURE [dbo].[OrganizationReport_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
    SET NOCOUNT ON;

    IF @Id IS NULL
       THROW 50000, 'Id cannot be null', 1;

    DELETE FROM [dbo].[OrganizationReport]
    WHERE [Id] = @Id
