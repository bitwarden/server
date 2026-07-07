using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationDomains.Interfaces;
using Bit.Core.Entities;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Utilities;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationDomains;

public class OrganizationDomainAllowEmailChangeQuery(
    IOrganizationRepository organizationRepository,
    IOrganizationDomainRepository organizationDomainRepository)
    : IOrganizationDomainAllowEmailChangeQuery
{
    /// <inheritdoc />
    public async Task ValidateAllowedAsync(User user, string newEmail)
    {
        var newDomain = EmailValidation.GetDomain(newEmail);

        // If the user is claimed by any active organization that uses organization domains,
        // the new domain must be one of those organizations' verified domains.
        var organizationsWithVerifiedUserEmailDomain =
            await organizationRepository.GetByVerifiedUserEmailDomainAsync(user.Id);

        var claimingOrganizations = organizationsWithVerifiedUserEmailDomain
            .Where(organization => organization is { Enabled: true, UseOrganizationDomains: true })
            .Select(organization => organization.Id)
            .ToList();

        if (claimingOrganizations.Count > 0)
        {
            var verifiedDomains = await organizationDomainRepository
                .GetVerifiedDomainsByOrganizationIdsAsync(claimingOrganizations);

            if (!verifiedDomains.Any(verifiedDomain => verifiedDomain.DomainName == newDomain))
            {
                throw new BadRequestException(
                    "Your account is managed by an organization, and this email address isn't on one of the organization's verified domains.");
            }

            return;
        }

        // Unclaimed user — fall back to the global block-policy check.
        var isDomainBlocked = await organizationDomainRepository
            .HasVerifiedDomainWithBlockClaimedDomainPolicyAsync(newDomain);
        if (isDomainBlocked)
        {
            throw new BadRequestException(
                "This email address is claimed by an organization using Bitwarden.");
        }
    }
}
