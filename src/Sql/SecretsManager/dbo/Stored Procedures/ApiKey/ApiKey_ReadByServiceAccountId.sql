CREATE PROCEDURE [dbo].[ApiKey_ReadByServiceAccountId]
    @ServiceAccountId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[ApiKeyView]
    WHERE
        [ServiceAccountId] = @ServiceAccountId
END
