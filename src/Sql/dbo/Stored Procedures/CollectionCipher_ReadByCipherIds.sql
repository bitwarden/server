CREATE PROCEDURE [dbo].[CollectionCipher_ReadByCipherIds]
    @CipherIds AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        CC.*
    FROM
        [dbo].[CollectionCipher] CC
    INNER JOIN
        @CipherIds C ON C.[Id] = CC.[CipherId]
END
