IF OBJECT_ID('[dbo].[User_Search]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[User_Search]
END
GO

CREATE PROCEDURE [dbo].[User_Search]
    @Email NVARCHAR(50),
    @Skip INT = 0,
    @Take INT = 25
AS
BEGIN
    SET NOCOUNT ON
    DECLARE @EmailLikeSearch NVARCHAR(55) = @Email + '%'

    SELECT
        *
    FROM
        [dbo].[UserView]
    WHERE
        (@Email IS NULL OR [Email] LIKE @EmailLikeSearch)
    ORDER BY [Email] ASC
    OFFSET @Skip ROWS
    FETCH NEXT @Take ROWS ONLY
END
GO

IF OBJECT_ID('[dbo].[Organization_Search]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[Organization_Search]
END
GO

CREATE PROCEDURE [dbo].[Organization_Search]
    @Name NVARCHAR(50),
    @UserEmail NVARCHAR(50),
    @Paid BIT,
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
            [dbo].[OrganizationView] O
        INNER JOIN
            [dbo].[OrganizationUser] OU ON O.[Id] = OU.[OrganizationId]
        INNER JOIN
            [dbo].[User] U ON U.[Id] = OU.[UserId]
        WHERE
            (@Name IS NULL OR O.[Name] LIKE @NameLikeSearch)
            AND (@UserEmail IS NULL OR U.[Email] = @UserEmail)
            AND
            (
                @Paid IS NULL OR
                (
                    (@Paid = 1 AND O.[GatewaySubscriptionId] IS NOT NULL) OR
                    (@Paid = 0 AND O.[GatewaySubscriptionId] IS NULL)
                )
            )
        ORDER BY O.[CreationDate] DESC
        OFFSET @Skip ROWS
        FETCH NEXT @Take ROWS ONLY
    END
    ELSE
    BEGIN
        SELECT
            O.*
        FROM
            [dbo].[OrganizationView] O
        WHERE
            (@Name IS NULL OR O.[Name] LIKE @NameLikeSearch)
            AND
            (
                @Paid IS NULL OR
                (
                    (@Paid = 1 AND O.[GatewaySubscriptionId] IS NOT NULL) OR
                    (@Paid = 0 AND O.[GatewaySubscriptionId] IS NULL)
                )
            )
        ORDER BY O.[CreationDate] DESC
        OFFSET @Skip ROWS
        FETCH NEXT @Take ROWS ONLY
    END
END
GO
