CREATE PROCEDURE [dbo].[OrganizationUser_ReadByOrganizationIdWithClaimedDomains]
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
