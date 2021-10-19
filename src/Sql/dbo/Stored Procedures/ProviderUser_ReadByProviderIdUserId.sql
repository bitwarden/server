CREATE PROCEDURE [dbo].[ProviderUser_ReadByProviderIdUserId]
    @ProviderId UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        *
    FROM
        [dbo].[ProviderUserView]
    WHERE
        [ProviderId] = @ProviderId
        AND [UserId] = @UserId
END
