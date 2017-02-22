CREATE VIEW [dbo].[CipherShareView]
AS
SELECT
    C.*,
    S.[Key],
    S.[Permissions],
    S.[Status]
FROM
    [dbo].[Cipher] C
LEFT JOIN
    [dbo].[Share] S ON C.[Id] = S.[CipherId]