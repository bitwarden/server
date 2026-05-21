using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationDomains.Interfaces;
using Bit.Core.Entities;
using Bit.Core.Repositories;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationDomains;

public class OrganizationDomainAllowEmailChangeQuery(
    IOrganizationRepository organizationRepository,
    IOrganizationDomainRepository organizationDomainRepository)
    : IOrganizationDomainAllowEmailChangeQuery
{
    private readonly IOrganizationRepository _organizationRepository = organizationRepository;
    private readonly IOrganizationDomainRepository _organizationDomainRepository = organizationDomainRepository;

    /// <inheritdoc />
    public async Task<bool> IsAllowedAsync(User user, string newEmailDomain)
    {
        // We want to see if the user is currently a claimed account
        var organizationsWithVerifiedUserEmailDomain =
            await _organizationRepository.GetByVerifiedUserEmailDomainAsync(user.Id);

        var claimingOrganizations = organizationsWithVerifiedUserEmailDomain
            .Where(organization => organization is { Enabled: true, UseOrganizationDomains: true });

        // If their account is claimed we need to apply those organization rules to the newEmailDomain.
        if (claimingOrganizations.Any())
        {
            var verifiedDomains = await _organizationDomainRepository.GetVerifiedDomainsByOrganizationIdsAsync(
                claimingOrganizations.Select(org => org.Id));

            return verifiedDomains.Any(verifiedDomain => verifiedDomain.DomainName == newEmailDomain);
        }

        // User is not claimed — fall back to the global block-policy check.
        var isDomainBlocked = await _organizationDomainRepository.HasVerifiedDomainWithBlockClaimedDomainPolicyAsync(newEmailDomain);
        return !isDomainBlocked;
    }
}
