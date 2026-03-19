CREATE PROCEDURE [dbo].[ApiKey_ReadByClientSecretHash]
    @ClientSecretHash VARCHAR(128)
AS
BEGIN
    SET NOCOUNT ON

    SELECT TOP 1
        *
    FROM
        [dbo].[ApiKeyView]
    WHERE
        [ClientSecretHash] = @ClientSecretHash
END
