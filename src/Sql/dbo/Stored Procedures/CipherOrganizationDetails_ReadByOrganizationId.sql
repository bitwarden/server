CREATE PROCEDURE [dbo].[CipherOrganizationDetails_ReadByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        C.*,
        CASE 
            WHEN O.[UseTotp] = 1 THEN 1
            ELSE 0
        END [OrganizationUseTotp]
    FROM
        [dbo].[CipherView] C
    LEFT JOIN
        [dbo].[OrganizationView] O ON O.[Id] = C.[OrganizationId]
    WHERE
        C.[OrganizationId] = @OrganizationId 
END
