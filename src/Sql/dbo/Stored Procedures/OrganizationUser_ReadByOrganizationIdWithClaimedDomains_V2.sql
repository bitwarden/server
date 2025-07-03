CREATE PROCEDURE [dbo].[OrganizationUser_ReadByOrganizationIdWithClaimedDomains_V2]
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
