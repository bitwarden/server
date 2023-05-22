CREATE VIEW [dbo].[ProviderUserUserDetailsView]
AS
SELECT
    PU.[Id],
    PU.[UserId],
    PU.[ProviderId],
    U.[Name],
    ISNULL(U.[Email], PU.[Email]) Email,
    PU.[Status],
    PU.[Type],
    PU.[Permissions],
    CASE WHEN U.[MasterPassword] IS NOT NULL THEN 1 ELSE 0 END AS HasMasterPassword
FROM
    [dbo].[ProviderUser] PU
LEFT JOIN
    [dbo].[User] U ON U.[Id] = PU.[UserId]
