CREATE OR ALTER PROCEDURE [dbo].[OrganizationUser_ReadOrganizationUserIdsManagedByOrganizationId]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    SELECT OU.[Id]
    FROM [dbo].[OrganizationUser] OU
    INNER JOIN [dbo].[User] U ON OU.[UserId] = U.[Id]
    WHERE OU.[OrganizationId] = @OrganizationId
    AND EXISTS (
        SELECT 1
        FROM [dbo].[OrganizationDomain] OD
        WHERE OD.[OrganizationId] = @OrganizationId
            AND OD.[VerifiedDate] IS NOT NULL
            AND U.[Email] LIKE '%@' + OD.[DomainName]
    );
END
GO

CREATE OR ALTER PROCEDURE [dbo].[User_IsManagedByAnyOrganization]
    @Id UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    SELECT CASE
        WHEN EXISTS (
            SELECT 1
            FROM [dbo].[User] U
            INNER JOIN [dbo].[OrganizationUser] OU ON U.[Id] = OU.[UserId]
            INNER JOIN [dbo].[OrganizationDomain] OD ON OU.[OrganizationId] = OD.[OrganizationId]
            WHERE U.[Id] = @Id
                AND OD.[VerifiedDate] IS NOT NULL
                AND U.[Email] LIKE '%@' + OD.[DomainName]
        )
        THEN 1
        ELSE 0
    END AS IsManaged;
END
GO
