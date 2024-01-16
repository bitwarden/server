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
    WHERE
        C.[UserId] IS NULL
        AND C.[OrganizationId] = @OrganizationId
        AND C.[Id] NOT IN (
            SELECT
                CC.[CipherId]
            FROM
                [dbo].[CollectionCipher] CC
            INNER JOIN
                [dbo].[Collection] S ON S.[Id] = CC.[CollectionId]
            WHERE
                S.[OrganizationId] = @OrganizationId
        )
END
GO
