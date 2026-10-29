CREATE PROCEDURE [dbo].[ApiKey_OrganizationDeleted]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[ApiKey]
    WHERE
        [OrganizationId] = @OrganizationId
END
