CREATE PROCEDURE [dbo].[OrganizationUser_ReadByOrganizationIdWithClaimedDomains_V2]
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
