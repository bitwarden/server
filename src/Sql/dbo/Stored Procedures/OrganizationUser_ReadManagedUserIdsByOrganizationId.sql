CREATE PROCEDURE [dbo].[OrganizationUser_ReadManagedUserIdsByOrganizationId]
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
