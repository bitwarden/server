CREATE PROCEDURE [dbo].[VerifiedOrganizationDomainSsoDetails_ReadByEmail]
@Email NVARCHAR(256)
AS
BEGIN
    SET NOCOUNT ON

    DECLARE @Domain NVARCHAR(256)

    SELECT @Domain = SUBSTRING(@Email, CHARINDEX( '@', @Email) + 1, LEN(@Email))

    SELECT
        O.Id AS OrganizationId,
        O.Name AS OrganizationName,
        O.Identifier AS OrganizationIdentifier,
        OD.DomainName
    FROM [dbo].[OrganizationView] O
             INNER JOIN [dbo].[OrganizationDomainView] OD ON O.Id = OD.OrganizationId
             LEFT JOIN [dbo].[Ssoconfig] S ON O.Id = S.OrganizationId
    WHERE OD.DomainName = @Domain
      AND O.Enabled = 1
      AND OD.VerifiedDate IS NOT NULL
      AND S.Enabled = 1
END
