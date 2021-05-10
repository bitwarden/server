CREATE PROCEDURE [dbo].[Organization_ReadByProviderId]
    @ProviderId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        O.*
    FROM
        [dbo].[OrganizationView] O
    INNER JOIN
        [dbo].[ProviderOrganization] UO ON O.[Id] = UO.[OrganizationId]
    WHERE
        UO.[ProviderId] = @ProviderId
END
