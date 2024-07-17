CREATE VIEW [dbo].[OrganizationUserUserDetailsWithPremiumAccessView]
AS
SELECT
    OU.[Id],
    OU.[UserId],
    OU.[OrganizationId],
    U.[Name],
    ISNULL(U.[Email], OU.[Email]) Email,
    U.[AvatarColor],
    U.[TwoFactorProviders],
    U.[Premium],
    OU.[Status],
    OU.[Type],
    OU.[AccessAll],
    OU.[AccessSecretsManager],
    OU.[ExternalId],
    SU.[ExternalId] SsoExternalId,
    OU.[Permissions],
    OU.[ResetPasswordKey],
    U.[UsesKeyConnector],
    CASE WHEN U.[MasterPassword] IS NOT NULL THEN 1 ELSE 0 END AS HasMasterPassword,
    CASE
        WHEN U.[Premium] = 1
            OR EXISTS (
                SELECT 1
                FROM [dbo].[Organization] O
                INNER JOIN [dbo].[OrganizationUser] OU2 ON OU2.[OrganizationId] = O.[Id]
                WHERE OU2.[UserId] = U.[Id]
                AND O.[UsersGetPremium] = 1
                AND O.[Enabled] = 1
            )
            THEN 1
        ELSE 0
        END AS HasPremiumAccess
FROM
    [dbo].[OrganizationUser] OU
LEFT JOIN
    [dbo].[User] U ON U.[Id] = OU.[UserId]
LEFT JOIN
    [dbo].[SsoUser] SU ON SU.[UserId] = OU.[UserId] AND SU.[OrganizationId] = OU.[OrganizationId]
