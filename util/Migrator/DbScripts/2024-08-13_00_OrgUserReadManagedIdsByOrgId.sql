CREATE OR ALTER PROCEDURE [dbo].[OrganizationUser_ReadManagedIdsByOrganizationId]
    @organizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    SELECT OU.[Id]
    FROM [dbo].[OrganizationUser] OU
    INNER JOIN [dbo].[User] U ON OU.[UserId] = U.[Id]
    WHERE OU.[OrganizationId] = @organizationId
    AND EXISTS (
        SELECT 1
        FROM [dbo].[OrganizationDomain] OD
        WHERE OD.[OrganizationId] = @organizationId
            AND OD.[VerifiedDate] IS NOT NULL
            AND OD.[DomainName] = SUBSTRING(U.[Email], CHARINDEX('@', U.[Email]) + 1, LEN(U.[Email]))
    );
END
GO
