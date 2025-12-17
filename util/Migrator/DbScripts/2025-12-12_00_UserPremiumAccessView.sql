-- Add UsersGetPremium to IX_Organization_Enabled index to support premium access queries

IF EXISTS (
    SELECT * FROM sys.indexes 
    WHERE name = 'IX_Organization_Enabled' 
    AND object_id = OBJECT_ID('[dbo].[Organization]')
)
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Organization_Enabled]
    ON [dbo].[Organization]([Id] ASC, [Enabled] ASC)
    INCLUDE ([UseTotp], [UsersGetPremium])
    WITH (DROP_EXISTING = ON);
END
ELSE
BEGIN
    CREATE NONCLUSTERED INDEX [IX_Organization_Enabled]
    ON [dbo].[Organization]([Id] ASC, [Enabled] ASC)
    INCLUDE ([UseTotp], [UsersGetPremium]);
END
GO

CREATE OR ALTER VIEW [dbo].[UserPremiumAccessView]
AS
SELECT 
    U.[Id],
    U.[Premium] AS [PersonalPremium],
    CAST(
        MAX(CASE 
            WHEN O.[Id] IS NOT NULL THEN 1 
            ELSE 0 
        END) AS BIT
    ) AS [OrganizationPremium]
FROM 
    [dbo].[User] U
LEFT JOIN 
    [dbo].[OrganizationUser] OU ON OU.[UserId] = U.[Id]
LEFT JOIN 
    [dbo].[Organization] O ON O.[Id] = OU.[OrganizationId]
    AND O.[UsersGetPremium] = 1
    AND O.[Enabled] = 1
GROUP BY 
    U.[Id], U.[Premium];
GO

CREATE OR ALTER PROCEDURE [dbo].[User_ReadPremiumAccessByIds]
    @Ids [dbo].[GuidIdArray] READONLY
AS
BEGIN
    SET NOCOUNT ON

    SELECT
        UPA.[Id],
        UPA.[PersonalPremium],
        UPA.[OrganizationPremium]
    FROM
        [dbo].[UserPremiumAccessView] UPA
    WHERE
        UPA.[Id] IN (SELECT [Id] FROM @Ids)
END
GO
