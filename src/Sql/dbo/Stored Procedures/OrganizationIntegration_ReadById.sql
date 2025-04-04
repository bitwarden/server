CREATE PROCEDURE [dbo].[OrganizationIntegration_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[OrganizationIntegration]
    WHERE
        [Id] = @Id
END
