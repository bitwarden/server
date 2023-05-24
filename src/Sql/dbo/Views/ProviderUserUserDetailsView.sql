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
    PU.[Permissions]
FROM
    [dbo].[ProviderUser] PU
LEFT JOIN
    [dbo].[User] U ON U.[Id] = PU.[UserId]
