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
        [dbo].[ProviderOrganization] PO ON O.[Id] = PO.[OrganizationId]
    WHERE
        PO.[ProviderId] = @ProviderId
END
