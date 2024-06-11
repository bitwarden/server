CREATE OR ALTER PROCEDURE [dbo].[CipherOrganizationDetails_ReadUnassignedByOrganizationId]
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
    LEFT JOIN
        [dbo].[CollectionCipher] CC ON C.[Id] = CC.[CipherId]
    LEFT JOIN
        [dbo].[Collection] S ON S.[Id] = CC.[CollectionId]
        AND S.[OrganizationId] = C.[OrganizationId]
    WHERE
        C.[UserId] IS NULL
        AND C.[OrganizationId] = @OrganizationId
        AND CC.[CipherId] IS NULL
END
GO
