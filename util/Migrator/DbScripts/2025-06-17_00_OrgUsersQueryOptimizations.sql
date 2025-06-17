CREATE OR ALTER VIEW [dbo].[UserEmailDomainView]
WITH SCHEMABINDING
AS
SELECT 
    Id,
    Email,
    SUBSTRING(Email, CHARINDEX('@', Email) + 1, LEN(Email)) AS EmailDomain
FROM dbo.[User]
WHERE Email IS NOT NULL 
    AND CHARINDEX('@', Email) > 0
GO

CREATE UNIQUE CLUSTERED INDEX IX_UserEmailDomainView_Id 
ON dbo.UserEmailDomainView (Id);
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationUserUserDetails_ReadByOrganizationId_V2]
    @OrganizationId UNIQUEIDENTIFIER,
    @IncludeGroups BIT = 0,
    @IncludeCollections BIT = 0
AS
BEGIN
    SET NOCOUNT ON

    -- Result Set 1: User Details (always returned)
    SELECT * 
    FROM [dbo].[OrganizationUserUserDetailsView] 
    WHERE OrganizationId = @OrganizationId

    -- Result Set 2: Group associations (if requested)
    IF @IncludeGroups = 1
    BEGIN
        SELECT gu.*
        FROM [dbo].[GroupUser] gu
        INNER JOIN [dbo].[OrganizationUser] ou ON gu.OrganizationUserId = ou.Id
        WHERE ou.OrganizationId = @OrganizationId
    END

    -- Result Set 3: Collection associations (if requested)  
    IF @IncludeCollections = 1
    BEGIN
        SELECT cu.*
        FROM [dbo].[CollectionUser] cu
        INNER JOIN [dbo].[OrganizationUser] ou ON cu.OrganizationUserId = ou.Id
        WHERE ou.OrganizationId = @OrganizationId
    END
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationUser_ReadByOrganizationIdWithClaimedDomains_V2]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    SELECT OU.*
    FROM [dbo].[OrganizationUserView] OU
    INNER JOIN [dbo].[UserEmailDomainView] U ON OU.[UserId] = U.[Id]
    INNER JOIN [dbo].[OrganizationDomainView] OD ON OU.[OrganizationId] = OD.[OrganizationId]
    WHERE OU.[OrganizationId] = @OrganizationId
      AND OD.[VerifiedDate] IS NOT NULL
      AND U.EmailDomain = OD.[DomainName]
END
GO
