CREATE VIEW [dbo].[ProviderAbilityView]
AS
SELECT
    [Id],
    [UseEvents],
    [Enabled]
FROM
    [dbo].[Provider]
