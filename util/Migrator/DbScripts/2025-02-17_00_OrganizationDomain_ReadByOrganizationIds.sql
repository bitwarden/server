
CREATE OR ALTER PROCEDURE [dbo].[OrganizationDomain_ReadByOrganizationIds]
    @OrganizationIds AS [dbo].[GuidIdArray] READONLY
AS
BEGIN

    SET NOCOUNT ON
        
    SELECT
        d.OrganizationId,
        d.DomainName
    FROM dbo.OrganizationDomainView AS d
    WHERE d.OrganizationId IN (SELECT [Id] FROM @OrganizationIds)
        AND d.VerifiedDate IS NOT NULL;
END