CREATE PROCEDURE [dbo].[ProviderOrganization_ReadByOrganizationIds]
    @Ids AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON
        
    IF (SELECT COUNT(1) FROM @Ids) < 1
    BEGIN
        RETURN(-1)
    END

    SELECT
        *
    FROM
        [dbo].[ProviderOrganizationView]
    WHERE
        [OrganizationId] IN (SELECT [Id] FROM @Ids)
END