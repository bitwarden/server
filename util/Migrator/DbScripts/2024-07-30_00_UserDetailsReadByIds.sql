CREATE OR ALTER PROCEDURE [dbo].[UserDetails_ReadByIds]
    @Ids AS [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    IF (SELECT COUNT(1) FROM @Ids) < 1
    BEGIN
        RETURN(-1)
    END

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
        [dbo].[UserView] U
    WHERE
        U.[Id] IN (SELECT [Id] FROM @Ids)
END
