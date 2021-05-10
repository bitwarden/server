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
        [dbo].[ProviderUser] OU ON OU.[UserId] = U.[Id]
    WHERE
        OU.[Id] = @ProviderUserId
        AND OU.[Status] = 2 -- Confirmed
END
