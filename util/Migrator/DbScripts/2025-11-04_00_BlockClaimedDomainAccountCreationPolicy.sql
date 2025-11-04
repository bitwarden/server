-- Add stored procedure for checking if a domain has the BlockClaimedDomainAccountCreation policy enabled
-- This supports the BlockClaimedDomainAccountCreation policy (Type = 19) which prevents users from
-- creating personal accounts using email addresses from domains claimed by organizations.

IF OBJECT_ID('[dbo].[OrganizationDomain_HasVerifiedDomainWithBlockPolicy]') IS NOT NULL
BEGIN
    DROP PROCEDURE [dbo].[OrganizationDomain_HasVerifiedDomainWithBlockPolicy]
END
GO

CREATE PROCEDURE [dbo].[OrganizationDomain_HasVerifiedDomainWithBlockPolicy]
    @DomainName NVARCHAR(255)
AS
BEGIN
    SET NOCOUNT ON

    -- Check if any organization has a verified domain matching the domain name
    -- with the BlockClaimedDomainAccountCreation policy enabled (Type = 19)
    IF EXISTS (
        SELECT 1
        FROM [dbo].[OrganizationDomain] OD
        INNER JOIN [dbo].[Organization] O
            ON OD.OrganizationId = O.Id
        INNER JOIN [dbo].[Policy] P
            ON O.Id = P.OrganizationId
        WHERE OD.DomainName = @DomainName
            AND OD.VerifiedDate IS NOT NULL
            AND O.Enabled = 1
            AND P.Type = 19  -- BlockClaimedDomainAccountCreation
            AND P.Enabled = 1
    )
    BEGIN
        SELECT CAST(1 AS BIT) AS HasBlockPolicy
    END
    ELSE
    BEGIN
        SELECT CAST(0 AS BIT) AS HasBlockPolicy
    END
END
GO
