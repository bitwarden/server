CREATE PROCEDURE [dbo].[OrganizationDomain_ReadByClaimedDomain]
    @DomainName NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON
        
    SELECT 
        *
    FROM
        [dbo].[OrganizationDomain]
    WHERE
        [DomainName] = @DomainName
    AND
        [VerifiedDate] IS NOT NULL
END