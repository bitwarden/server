CREATE PROCEDURE [dbo].[OrganizationScopedApiKey_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationScopedApiKeyView]
    WHERE
        [Id] = @Id
END
