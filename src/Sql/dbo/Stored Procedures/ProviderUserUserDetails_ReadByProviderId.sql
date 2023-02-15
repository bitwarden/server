CREATE PROCEDURE [dbo].[ProviderUserUserDetails_ReadByProviderId]
    @ProviderId UNIQUEIDENTIFIER,
    @Status TINYINT = NULL
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[ProviderUserUserDetailsView]
    WHERE
        [ProviderId] = @ProviderId
        AND [Status] = COALESCE(@Status, [Status])
END
