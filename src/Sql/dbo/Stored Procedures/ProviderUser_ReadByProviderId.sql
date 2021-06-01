CREATE PROCEDURE [dbo].[ProviderUser_ReadByProviderId]
    @ProviderId UNIQUEIDENTIFIER,
    @Type TINYINT
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[ProviderUserView]
    WHERE
        [ProviderId] = @ProviderId
    AND (@Type IS NULL OR [Type] = @Type)
END
