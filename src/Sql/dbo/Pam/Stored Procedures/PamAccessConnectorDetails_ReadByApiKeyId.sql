CREATE PROCEDURE [dbo].[PamAccessConnectorDetails_ReadByApiKeyId]
    @ApiKeyId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[PamAccessConnectorDetailsView]
    WHERE
        [ApiKeyId] = @ApiKeyId
END
