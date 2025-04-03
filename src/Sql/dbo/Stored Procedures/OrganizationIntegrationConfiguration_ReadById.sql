CREATE PROCEDURE [dbo].[OrganizationIntegrationConfiguration_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationIntegrationConfiguration]
    WHERE
        [Id] = @Id
END
