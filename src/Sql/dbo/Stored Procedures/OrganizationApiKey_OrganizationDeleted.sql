CREATE PROCEDURE [dbo].[OrganizationApiKey_OrganizationDeleted]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[OrganizationApiKey]
    WHERE
        [OrganizationId] = @OrganizationId
END
