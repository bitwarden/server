CREATE PROCEDURE [dbo].[OrganizationScopedApiKey_OrganizationDeleted]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[OrganizationScopedApiKey]
    WHERE
        [OrganizationId] = @OrganizationId
END
