CREATE PROCEDURE [dbo].[ProviderUser_ReadCountByProviderIdEmail]
    @ProviderId UNIQUEIDENTIFIER,
    @Email NVARCHAR(256),
    @OnlyUsers BIT
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        COUNT(1)
    FROM
        [dbo].[ProviderUser] OU
    LEFT JOIN
        [dbo].[User] U ON OU.[UserId] = U.[Id]
    WHERE
        OU.[ProviderId] = @ProviderId
        AND (
            (@OnlyUsers = 0 AND @Email IN (OU.[Email], U.[Email]))
            OR (@OnlyUsers = 1 AND U.[Email] = @Email)
        )
END
