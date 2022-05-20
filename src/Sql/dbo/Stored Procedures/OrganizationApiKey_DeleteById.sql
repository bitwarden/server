CREATE PROCEDURE [dbo].[OrganizationApiKey_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE FROM [dbo].[OrganizationApiKey]
    WHERE [Id] = @Id
END
