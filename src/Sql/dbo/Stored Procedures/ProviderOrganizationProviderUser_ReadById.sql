CREATE PROCEDURE [dbo].[ProviderOrganizationProviderUser_ReadById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[ProviderOrganizationProviderUser]
    WHERE
        [Id] = @Id
END
