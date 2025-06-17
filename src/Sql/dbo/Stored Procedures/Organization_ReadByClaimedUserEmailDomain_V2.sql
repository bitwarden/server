CREATE PROCEDURE [dbo].[Organization_ReadByClaimedUserEmailDomain_V2]
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    SELECT O.*
    FROM dbo.[UserEmailDomainView] U
    INNER JOIN dbo.[OrganizationUserView] OU ON U.[Id] = OU.[UserId]
    INNER JOIN dbo.[OrganizationView] O ON OU.[OrganizationId] = O.[Id]
    INNER JOIN dbo.[OrganizationDomainView] OD ON OU.[OrganizationId] = OD.[OrganizationId]
    WHERE U.[Id] = @UserId
      AND OD.[VerifiedDate] IS NOT NULL
      AND U.EmailDomain = OD.[DomainName]
      AND O.[Enabled] = 1
END 