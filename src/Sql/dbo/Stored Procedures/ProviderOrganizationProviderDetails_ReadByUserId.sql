CREATE PROCEDURE [dbo].[ProviderOrganizationProviderDetails_ReadByUserId]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

SELECT
    PO.Id,
    PO.OrganizationId,
    PO.ProviderId,
    P.Name as ProviderName,
    P.[Type] as ProviderType
FROM
    [dbo].[ProviderOrganizationView] PO
INNER JOIN
    [dbo].[OrganizationUser] OU ON PO.OrganizationId = OU.OrganizationId
INNER JOIN
        [dbo].[Provider] P ON PO.ProviderId = P.Id
WHERE
    OU.UserId = @UserId
END
