CREATE PROCEDURE [dbo].[SsoConfig_ReadManyByOrganizationIds]
    @OrganizationIds AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        SC.*
    FROM
        [dbo].[SsoConfigView] SC
    INNER JOIN
        @OrganizationIds I ON SC.[OrganizationId] = I.[Id]
END
