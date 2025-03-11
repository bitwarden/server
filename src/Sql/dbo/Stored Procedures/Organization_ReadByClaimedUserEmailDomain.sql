CREATE PROCEDURE [dbo].[Organization_ReadByClaimedUserEmailDomain]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    WITH CTE_User AS (
        SELECT
            U.*,
            SUBSTRING(U.Email, CHARINDEX('@', U.Email) + 1, LEN(U.Email)) AS EmailDomain
        FROM dbo.[UserView] U
        WHERE U.[Id] = @UserId
    )
    SELECT O.*
    FROM CTE_User CU
             INNER JOIN dbo.[OrganizationUserView] OU ON CU.[Id] = OU.[UserId]
             INNER JOIN dbo.[OrganizationView] O ON OU.[OrganizationId] = O.[Id]
             INNER JOIN dbo.[OrganizationDomainView] OD ON OU.[OrganizationId] = OD.[OrganizationId]
    WHERE OD.[VerifiedDate] IS NOT NULL
      AND CU.EmailDomain = OD.[DomainName]
      AND O.[Enabled] = 1
END
