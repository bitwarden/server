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
            AND U.[Email] = COALESCE(@UserEmail, U.[Email])
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
