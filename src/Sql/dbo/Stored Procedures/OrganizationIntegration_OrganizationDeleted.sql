CREATE PROCEDURE [dbo].[OrganizationIntegration_OrganizationDeleted]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[OrganizationIntegration]
    WHERE
        [OrganizationId] = @OrganizationId
END
