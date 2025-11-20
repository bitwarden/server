CREATE OR ALTER VIEW [dbo].[CollectionCipherDetailsView]
AS
SELECT
    CC.[CollectionId],
    C.[OrganizationId] AS [CollectionOrganizationId],
    CC.[CipherId],
    Ci.[OrganizationId] AS [CipherOrganizationId],
    Ci.[DeletedDate]
FROM
    [dbo].[CollectionCipher] CC
        INNER JOIN
    [dbo].[Collection] C ON C.[Id] = CC.[CollectionId]
        INNER JOIN
    [dbo].[Cipher] Ci ON Ci.[Id] = CC.[CipherId]
GO
