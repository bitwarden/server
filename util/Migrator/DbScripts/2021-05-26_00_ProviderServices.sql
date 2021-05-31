ALTER TABLE [dbo].[Provider] ALTER COLUMN [Name] NVARCHAR (50) NULL;
GO

ALTER TABLE [dbo].[Provider] ALTER COLUMN [BillingEmail] NVARCHAR (256) NULL;
GO

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
GO

IF OBJECT_ID('[dbo].[User_BumpAccountRevisionDateByProviderUserIds]') IS NOT NULL
    BEGIN
        DROP PROCEDURE [dbo].[User_BumpAccountRevisionDateByProviderUserIds]
    END
GO

CREATE PROCEDURE [dbo].[User_BumpAccountRevisionDateByProviderUserIds]
    @ProviderUserIds [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    UPDATE
        U
    SET
        U.[AccountRevisionDate] = GETUTCDATE()
    FROM
        @ProviderUserIds OUIDs
    INNER JOIN
        [dbo].[ProviderUser] PU ON OUIDs.Id = PU.Id AND PU.[Status] = 2 -- Confirmed
    INNER JOIN
        [dbo].[User] U ON PU.UserId = U.Id
END
GO

IF OBJECT_ID('[dbo].[ProviderUser_DeleteByIds]') IS NOT NULL
    BEGIN
        DROP PROCEDURE [dbo].[ProviderUser_DeleteByIds]
    END
GO

CREATE PROCEDURE [dbo].[ProviderUser_DeleteByIds]
@Ids [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    EXEC [dbo].[User_BumpAccountRevisionDateByProviderUserIds] @Ids

    DECLARE @UserAndProviderIds [dbo].[TwoGuidIdArray]

    INSERT INTO @UserAndProviderIds
    (Id1, Id2)
    SELECT
        UserId,
        ProviderId
    FROM
        [dbo].[ProviderUser] PU
            INNER JOIN
        @Ids PUIds ON PUIds.Id = PU.Id
    WHERE
        UserId IS NOT NULL AND
        ProviderId IS NOT NULL

    DECLARE @BatchSize INT = 100

    -- Delete ProviderUsers
    WHILE @BatchSize > 0
        BEGIN
            BEGIN TRANSACTION ProviderUser_DeleteMany_PUs

                DELETE TOP(@BatchSize) OU
                FROM
                    [dbo].[ProviderUser] PU
                        INNER JOIN
                    @Ids I ON I.Id = PU.Id

                SET @BatchSize = @@ROWCOUNT

            COMMIT TRANSACTION ProviderUser_DeleteMany_PUs
        END
END
GO

IF OBJECT_ID('[dbo].[Provider_Search]') IS NOT NULL
    BEGIN
        DROP PROCEDURE [dbo].[Provider_Search]
    END
GO

CREATE PROCEDURE [dbo].[Provider_Search]
    @Name NVARCHAR(50),
    @UserEmail NVARCHAR(256),
    @Skip INT = 0,
    @Take INT = 25
    WITH RECOMPILE
AS
BEGIN
    SET NOCOUNT ON
    DECLARE @NameLikeSearch NVARCHAR(55) = '%' + @Name + '%'

    IF @UserEmail IS NOT NULL
        BEGIN
            SELECT
                O.*
            FROM
                [dbo].[ProviderView] O
                    INNER JOIN
                [dbo].[ProviderUser] OU ON O.[Id] = OU.[ProviderId]
                    INNER JOIN
                [dbo].[User] U ON U.[Id] = OU.[UserId]
            WHERE
                (@Name IS NULL OR O.[Name] LIKE @NameLikeSearch)
              AND (@UserEmail IS NULL OR U.[Email] = @UserEmail)
            ORDER BY O.[CreationDate] DESC
            OFFSET @Skip ROWS
                FETCH NEXT @Take ROWS ONLY
        END
    ELSE
        BEGIN
            SELECT
                O.*
            FROM
                [dbo].[ProviderView] O
            WHERE
                (@Name IS NULL OR O.[Name] LIKE @NameLikeSearch)
            ORDER BY O.[CreationDate] DESC
            OFFSET @Skip ROWS
                FETCH NEXT @Take ROWS ONLY
        END
END

