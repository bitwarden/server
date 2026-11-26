CREATE OR ALTER PROCEDURE [dbo].[CollectionCipher_ReadByCipherIds]
    @CipherIds AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        CC.[CollectionId],
        CC.[CipherId]
    FROM
        [dbo].[CollectionCipher] CC
    INNER JOIN
        @CipherIds C ON C.[Id] = CC.[CipherId]
END
GO
