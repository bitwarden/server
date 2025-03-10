IF NOT EXISTS(SELECT name
FROM sys.indexes
WHERE name = 'IX_OrganizationDomain_OrganizationIdVerifiedDate')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_OrganizationDomain_OrganizationIdVerifiedDate]
        ON [dbo].[OrganizationDomain] ([OrganizationId],[VerifiedDate]);
END
GO

IF NOT EXISTS(SELECT name
FROM sys.indexes
WHERE name = 'IX_OrganizationDomain_VerifiedDate')
BEGIN
    CREATE NONCLUSTERED INDEX [IX_OrganizationDomain_VerifiedDate]
        ON [dbo].[OrganizationDomain] ([VerifiedDate])
        INCLUDE ([OrganizationId],[DomainName]);
END
GO

CREATE OR ALTER PROCEDURE [dbo].[OrganizationUser_ReadByOrganizationIdWithClaimedDomains]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    SELECT OU.*
    FROM [dbo].[OrganizationUserView] OU
    INNER JOIN [dbo].[UserView] U ON OU.[UserId] = U.[Id]
    WHERE OU.[OrganizationId] = @OrganizationId
        AND EXISTS (
            SELECT 1
            FROM [dbo].[OrganizationDomainView] OD
            WHERE OD.[OrganizationId] = @OrganizationId
                AND OD.[VerifiedDate] IS NOT NULL
                AND U.[Email] LIKE '%@' + OD.[DomainName]
        );
END
GO

CREATE OR ALTER PROCEDURE [dbo].[Organization_ReadByClaimedUserEmailDomain]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    SELECT O.*
    FROM [dbo].[UserView] U
    INNER JOIN [dbo].[OrganizationUserView] OU ON U.[Id] = OU.[UserId]
    INNER JOIN [dbo].[OrganizationView] O ON OU.[OrganizationId] = O.[Id]
    INNER JOIN [dbo].[OrganizationDomainView] OD ON OU.[OrganizationId] = OD.[OrganizationId]
    WHERE U.[Id] = @UserId
        AND OD.[VerifiedDate] IS NOT NULL
        AND U.[Email] LIKE '%@' + OD.[DomainName];
END
GO
