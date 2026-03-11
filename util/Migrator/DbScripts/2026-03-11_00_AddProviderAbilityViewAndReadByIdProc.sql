CREATE OR ALTER VIEW [dbo].[ProviderAbilityView]
AS
SELECT
    [Id],
    [UseEvents],
    [Enabled]
FROM
    [dbo].[Provider]
GO

CREATE OR ALTER PROCEDURE [dbo].[Provider_ReadAbilityById]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        [Id],
        [UseEvents],
        [Enabled]
    FROM
        [dbo].[ProviderAbilityView]
    WHERE
        [Id] = @Id
END
GO
