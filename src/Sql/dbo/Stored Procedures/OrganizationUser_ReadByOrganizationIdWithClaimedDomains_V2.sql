CREATE PROCEDURE [dbo].[OrganizationUser_ReadByOrganizationIdWithClaimedDomains_V2]
    @OrganizationId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    WITH CTE_UserWithDomain AS (
        SELECT
            OU.*,
            SUBSTRING(U.Email, CHARINDEX('@', U.Email) + 1, LEN(U.Email)) AS EmailDomain
        FROM [dbo].[OrganizationUserView] OU
        INNER JOIN [dbo].[UserView] U ON OU.[UserId] = U.[Id]
        WHERE OU.[OrganizationId] = @OrganizationId
    )
    SELECT OU.*
    FROM CTE_UserWithDomain OU
    INNER JOIN [dbo].[OrganizationDomainView] OD ON OU.[OrganizationId] = OD.[OrganizationId]
    WHERE OD.[VerifiedDate] IS NOT NULL
      AND OU.EmailDomain = OD.[DomainName]
END
