CREATE PROCEDURE [dbo].[ProviderOrganization_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[ProviderOrganizationView]
    WHERE
        [Id] = @Id
END
