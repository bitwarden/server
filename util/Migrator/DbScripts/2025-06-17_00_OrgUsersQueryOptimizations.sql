CREATE OR ALTER VIEW [dbo].[UserEmailDomainView]
AS
SELECT 
    Id,
    Email,
    SUBSTRING(Email, CHARINDEX('@', Email) + 1, LEN(Email)) AS EmailDomain
FROM dbo.[User]
WHERE Email IS NOT NULL 
    AND CHARINDEX('@', Email) > 0
GO

IF NOT EXISTS(SELECT name FROM sys.indexes WHERE name = 'IX_OrganizationUser_OrgId_UserId_Includes')
    BEGIN
        CREATE NONCLUSTERED INDEX [IX_OrganizationUser_OrgId_UserId_Includes]
            ON [dbo].[OrganizationUser] ([OrganizationId], [UserId])
            INCLUDE ([Id], [Email], [Key], [Status], [Type], [ExternalId], [CreationDate], [RevisionDate], [Permissions], [ResetPasswordKey], [AccessSecretsManager])
    END
GO

IF NOT EXISTS(SELECT name FROM sys.indexes WHERE name = 'IX_User_Id_EmailDomain')
    BEGIN
        CREATE NONCLUSTERED INDEX [IX_User_Id_EmailDomain]
            ON [dbo].[User] ([Id], [Email])
    END
GO

IF NOT EXISTS(SELECT name FROM sys.indexes WHERE name = 'IX_OrganizationDomain_Org_VerifiedDomain')
    BEGIN
        CREATE NONCLUSTERED INDEX [IX_OrganizationDomain_Org_VerifiedDomain]
            ON [dbo].[OrganizationDomain] ([OrganizationId], [DomainName])
            WHERE [VerifiedDate] IS NOT NULL
    END
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

    WITH OrgUsers AS (
        SELECT *
        FROM [dbo].[OrganizationUserView]
        WHERE [OrganizationId] = @OrganizationId
    ),
    UserDomains AS (
        SELECT U.[Id], U.[EmailDomain]
        FROM [dbo].[UserEmailDomainView] U
        WHERE EXISTS (
            SELECT 1
            FROM [dbo].[OrganizationDomainView] OD WITH (INDEX(IX_OrganizationDomain_Org_VerifiedDomain))
            WHERE OD.[OrganizationId] = @OrganizationId
            AND OD.[VerifiedDate] IS NOT NULL
            AND OD.[DomainName] = U.[EmailDomain]
        )
    )
    SELECT OU.*
    FROM OrgUsers OU
    JOIN UserDomains UD ON OU.[UserId] = UD.[Id]
    OPTION (RECOMPILE);
END
GO
