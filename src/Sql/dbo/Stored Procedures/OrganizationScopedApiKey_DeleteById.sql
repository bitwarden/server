CREATE PROCEDURE [dbo].[OrganizationScopedApiKey_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[OrganizationScopedApiKey]
    WHERE
        [Id] = @Id
END
