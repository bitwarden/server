CREATE PROCEDURE [dbo].[Send_ReadItemSendsWithDeletedCipher]
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        S.*
    FROM
        [dbo].[SendView] S
    WHERE
        S.[Type] = 2
        AND S.[CipherId] IS NOT NULL
        AND NOT EXISTS (
            SELECT
                1
            FROM
                [dbo].[Cipher] C
            WHERE
                C.[Id] = S.[CipherId]
        )
END
