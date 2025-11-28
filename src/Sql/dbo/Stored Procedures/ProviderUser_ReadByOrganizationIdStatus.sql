CREATE PROCEDURE [dbo].[ProviderUser_ReadByOrganizationIdStatus]
    @OrganizationId UNIQUEIDENTIFIER,
    @Status TINYINT
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        PU.*
    FROM
        [dbo].[ProviderUserView] PU
    INNER JOIN [dbo].[ProviderOrganizationView] as PO
        ON PU.[ProviderId] = PO.[ProviderId]
    WHERE
        PO.[OrganizationId] = @OrganizationId
        AND (@Status IS NULL OR PU.[Status] = @Status)
END