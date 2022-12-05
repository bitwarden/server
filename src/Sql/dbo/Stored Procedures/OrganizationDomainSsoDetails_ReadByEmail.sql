CREATE PROCEDURE [dbo].[OrganizationDomainSsoDetails_ReadByEmail]
    @Email NVARCHAR(256)
AS
BEGIN
    SET NOCOUNT ON
        
    DECLARE @Domain NVARCHAR(256)
        
    SELECT @Domain = SUBSTRING(@Email, CHARINDEX( '@', @Email) + 1, LEN(@Email))

    SELECT
        O.Id AS OrganizationId,
        O.[Name] AS OrganizationName,
        O.UseSso AS SsoAvailable,
        P.Enabled AS SsoRequired,
        O.Identifier AS OrganizationIdentifier,
        OD.VerifiedDate,
        P.[Type] AS PolicyType,
        OD.DomainName
    FROM
        [dbo].[OrganizationView] O
    INNER JOIN [dbo].[OrganizationDomainView] OD
        ON O.Id = OD.OrganizationId
    INNER JOIN [dbo].[PolicyView] P
        ON O.Id = P.OrganizationId
    WHERE OD.DomainName = @Domain
    AND O.Enabled = 1
    AND P.[Type] = 4 -- SSO Type
END    