IF OBJECT_ID('[dbo].[ProviderUser_ReadCountByProviderIdEmail]') IS NOT NULL
    BEGIN
        DROP PROCEDURE [dbo].[ProviderUser_ReadCountByProviderIdEmail]
    END
GO

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
        (@OnlyUsers = 0 AND (OU.[Email] = @Email OR U.[Email] = @Email))
        OR (@OnlyUsers = 1 AND U.[Email] = @Email)
    )
END
GO

IF OBJECT_ID('[dbo].[ProviderUser_ReadByIds]') IS NOT NULL
    BEGIN
        DROP PROCEDURE [dbo].[ProviderUser_ReadByIds]
    END
GO

CREATE PROCEDURE [dbo].[ProviderUser_ReadByIds]
    @Ids AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    IF (SELECT COUNT(1) FROM @Ids) < 1
        BEGIN
            RETURN(-1)
        END

    SELECT
        *
    FROM
        [dbo].[providerUserView]
    WHERE
        [Id] IN (SELECT [Id] FROM @Ids)
END
