CREATE PROCEDURE [dbo].[OrganizationIntegration_ReadByOrganizationIdType]
    @OrganizationId UNIQUEIDENTIFIER,
    @Type SMALLINT
AS
BEGIN
    SET NOCOUNT ON

SELECT
    *
FROM
    [dbo].[OrganizationIntegrationView]
WHERE
    [OrganizationId] = @OrganizationId
    AND [Type] = @Type
END
