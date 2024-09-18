CREATE PROCEDURE [dbo].[Organization_ReadByClaimedUserEmailDomain]
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
