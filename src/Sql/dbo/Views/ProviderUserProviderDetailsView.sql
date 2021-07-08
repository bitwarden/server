CREATE VIEW [dbo].[ProviderUserProviderDetailsView]
AS
SELECT
    PU.[UserId],
    PU.[ProviderId],
    P.[Name],
    PU.[Key],
    PU.[Status],
    PU.[Type],
    P.[Enabled],
    PU.[Permissions],
    P.[UseEvents]
FROM
    [dbo].[ProviderUser] PU
LEFT JOIN
    [dbo].[Provider] P ON P.[Id] = PU.[ProviderId]
