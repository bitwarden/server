CREATE PROCEDURE [dbo].[OrganizationIntegration_DeleteById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    DELETE
    FROM
        [dbo].[OrganizationIntegration]
    WHERE
        [Id] = @Id
END
