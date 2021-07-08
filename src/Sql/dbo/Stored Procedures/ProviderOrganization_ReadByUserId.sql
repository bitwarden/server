CREATE PROCEDURE [dbo].[ProviderOrganization_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        PO.*
    FROM
        [dbo].[ProviderOrganizationView] PO
    INNER JOIN
        [dbo].[Provider] P ON PO.[ProviderId] = P.[Id]
    INNER JOIN
        [dbo].[ProviderUser] PU ON P.[Id] = PU.[ProviderId]
    WHERE
        PU.[UserId] = @UserId
END
