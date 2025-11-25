-- Create UserPremiumAccessView to centralize premium access calculation
CREATE OR ALTER VIEW [dbo].[UserPremiumAccessView]
AS
SELECT DISTINCT 
    U.[Id] AS UserId,
    CASE 
        WHEN U.[Premium] = 1 THEN 1
        WHEN EXISTS (
            SELECT 1
            FROM [dbo].[OrganizationUser] OU
            INNER JOIN [dbo].[Organization] O ON OU.[OrganizationId] = O.[Id]
            WHERE OU.[UserId] = U.[Id]
                AND O.[UsersGetPremium] = 1
                AND O.[Enabled] = 1
        )
        THEN 1
        ELSE 0
    END AS HasPremiumAccess
FROM 
    [dbo].[User] U
GO

-- Update OrganizationUserUserDetailsView to use UserPremiumAccessView
CREATE OR ALTER VIEW [dbo].[OrganizationUserUserDetailsView]
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
    OU.[AccessSecretsManager],
    OU.[ExternalId],
    SU.[ExternalId] SsoExternalId,
    OU.[Permissions],
    OU.[ResetPasswordKey],
    U.[UsesKeyConnector],
    CASE WHEN U.[MasterPassword] IS NOT NULL THEN 1 ELSE 0 END AS HasMasterPassword,
    ISNULL(UPA.[HasPremiumAccess], 0) AS HasPremiumAccess
FROM
    [dbo].[OrganizationUser] OU
LEFT JOIN
    [dbo].[User] U ON U.[Id] = OU.[UserId]
LEFT JOIN
    [dbo].[SsoUser] SU ON SU.[UserId] = OU.[UserId] AND SU.[OrganizationId] = OU.[OrganizationId]
LEFT JOIN
    [dbo].[UserPremiumAccessView] UPA ON UPA.[UserId] = U.[Id]
GO

-- Update User_ReadByIdsWithCalculatedPremium stored procedure to use UserPremiumAccessView
CREATE OR ALTER PROCEDURE [dbo].[User_ReadByIdsWithCalculatedPremium]
    @Ids NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;

    -- Declare a table variable to hold the parsed JSON data
    DECLARE @ParsedIds TABLE (Id UNIQUEIDENTIFIER);

    -- Parse the JSON input into the table variable
    INSERT INTO @ParsedIds (Id)
    SELECT value
    FROM OPENJSON(@Ids);

    -- Check if the input table is empty
    IF (SELECT COUNT(1) FROM @ParsedIds) < 1
    BEGIN
        RETURN(-1);
    END

    -- Main query to fetch user details and calculate premium access
    SELECT
        U.*,
        ISNULL(UPA.[HasPremiumAccess], 0) AS HasPremiumAccess
    FROM
        [dbo].[UserView] U
    LEFT JOIN
        [dbo].[UserPremiumAccessView] UPA ON UPA.[UserId] = U.[Id]
    WHERE
        U.[Id] IN (SELECT [Id] FROM @ParsedIds);
END;
GO

-- Refresh stored procedures that reference OrganizationUserUserDetailsView
EXEC sp_refreshsqlmodule N'[dbo].[OrganizationUserUserDetails_ReadByOrganizationId]';
EXEC sp_refreshsqlmodule N'[dbo].[OrganizationUserUserDetails_ReadByOrganizationId_V2]';
EXEC sp_refreshsqlmodule N'[dbo].[OrganizationUser_ReadManyDetailsByRole]';
EXEC sp_refreshsqlmodule N'[dbo].[OrganizationUser_ReadByMinimumRole]';
EXEC sp_refreshsqlmodule N'[dbo].[OrganizationUserUserDetails_ReadById]';
GO

