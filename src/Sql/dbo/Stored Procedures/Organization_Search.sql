CREATE PROCEDURE [dbo].[Organization_Search]
    @Name NVARCHAR(50),
    @UserEmail NVARCHAR(50),
    @Paid BIT,
    @Skip INT = 0,
    @Take INT = 25
AS
BEGIN
    SET NOCOUNT ON
    DECLARE @NameLikeSearch NVARCHAR(55) = '%' + @Name + '%'

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
