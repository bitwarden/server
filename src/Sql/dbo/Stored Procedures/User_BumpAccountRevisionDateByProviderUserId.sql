CREATE PROCEDURE [dbo].[User_BumpAccountRevisionDateByProviderUserId]
    @ProviderUserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        U
    SET
        U.[AccountRevisionDate] = GETUTCDATE()
    FROM
        [dbo].[User] U
    INNER JOIN
        [dbo].[ProviderUser] PU ON PU.[UserId] = U.[Id]
    WHERE
        PU.[Id] = @ProviderUserId
        AND PU.[Status] = 2 -- Confirmed
END
