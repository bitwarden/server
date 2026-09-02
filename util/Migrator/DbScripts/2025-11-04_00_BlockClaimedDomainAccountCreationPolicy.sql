-- Add stored procedure for checking if a domain has the BlockClaimedDomainAccountCreation policy enabled
-- This supports the BlockClaimedDomainAccountCreation policy (Type = 19) which prevents users from
-- creating personal accounts using email addresses from domains claimed by organizations.
-- The optional @ExcludeOrganizationId parameter allows excluding a specific organization from the check,
-- enabling users to join the organization that owns their email domain.

CREATE OR ALTER PROCEDURE [dbo].[OrganizationDomain_HasVerifiedDomainWithBlockPolicy]
    @DomainName NVARCHAR(255),
    @ExcludeOrganizationId UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON

    -- Check if any organization has a verified domain matching the domain name
    -- with the BlockClaimedDomainAccountCreation policy enabled (Type = 19)
    -- If @ExcludeOrganizationId is provided, exclude that organization from the check
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
            AND O.UsePolicies = 1
            AND O.UseOrganizationDomains = 1
            AND (@ExcludeOrganizationId IS NULL OR O.Id != @ExcludeOrganizationId)
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
