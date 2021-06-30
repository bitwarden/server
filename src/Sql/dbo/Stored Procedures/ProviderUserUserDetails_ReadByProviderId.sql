CREATE PROCEDURE [dbo].[ProviderUserUserDetails_ReadByProviderId]
    @ProviderId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[ProviderUserUserDetailsView]
    WHERE
        [ProviderId] = @ProviderId
END
