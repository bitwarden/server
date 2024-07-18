CREATE PROCEDURE [dbo].[UserDetails_Search]
    @Email NVARCHAR(256),
    @Skip INT = 0,
    @Take INT = 25
WITH RECOMPILE
AS
BEGIN
    SET NOCOUNT ON
    DECLARE @EmailLikeSearch NVARCHAR(261) = @Email + '%'

    SELECT
        U.*,
        CASE
            WHEN U.[Premium] = 1
                OR EXISTS (
                    SELECT 1
                    FROM [dbo].[OrganizationUser] OU
                        JOIN [dbo].[Organization] O ON OU.[OrganizationId] = O.[Id]
                        WHERE OU.[UserId] = U.[Id]
                          AND O.[UsersGetPremium] = 1
                          AND O.[Enabled] = 1
                )
            THEN 1
            ELSE 0
            END AS HasPremiumAccess
    FROM
        [dbo].[User] U
    WHERE
        (@Email IS NULL OR U.[Email] LIKE @EmailLikeSearch)
    ORDER BY U.[Email] ASC
    OFFSET @Skip ROWS
    FETCH NEXT @Take ROWS ONLY
END
