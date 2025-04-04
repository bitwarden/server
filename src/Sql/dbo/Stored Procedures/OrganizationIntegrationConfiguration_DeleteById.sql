CREATE PROCEDURE [dbo].[OrganizationIntegrationConfiguration_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[OrganizationIntegrationConfiguration]
    WHERE
        [Id] = @Id
END
