using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationDomains.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.OrganizationDomains.Interfaces;
using Bit.Core.Entities;
using Bit.Core.Repositories;

namespace Bit.Core.AdminConsole.OrganizationFeatures.OrganizationDomains;

public class OrganizationDomainAllowEmailChangeQuery(
    IOrganizationRepository organizationRepository,
    IOrganizationDomainRepository organizationDomainRepository)
    : IOrganizationDomainAllowEmailChangeQuery
{
    /// <inheritdoc />
    public async Task<OrganizationDomainAllowEmailChangeDenialReason> IsAllowedAsync(User user, string newEmailDomain)
    {
        // We want to see if the user is currently a claimed account
        var organizationsWithVerifiedUserEmailDomain =
            await organizationRepository.GetByVerifiedUserEmailDomainAsync(user.Id);

        var claimingOrganizations = organizationsWithVerifiedUserEmailDomain
            .Where(organization => organization is { Enabled: true, UseOrganizationDomains: true })
            .Select(organization => organization.Id);

        // If their account is claimed we need to apply those organization rules to the newEmailDomain.
        if (claimingOrganizations.Any())
        {
            var verifiedDomains = await organizationDomainRepository.GetVerifiedDomainsByOrganizationIdsAsync(
                claimingOrganizations);

            return verifiedDomains.Any(verifiedDomain => verifiedDomain.DomainName == newEmailDomain)
                ? OrganizationDomainAllowEmailChangeDenialReason.Allowed
                : OrganizationDomainAllowEmailChangeDenialReason.UserIsClaimedAndDomainNotVerified;
        }

        // User is not claimed — fall back to the global block-policy check.
        var isDomainBlocked = await organizationDomainRepository.HasVerifiedDomainWithBlockClaimedDomainPolicyAsync(newEmailDomain);
        return isDomainBlocked
            ? OrganizationDomainAllowEmailChangeDenialReason.DomainIsBlockedByPolicy
            : OrganizationDomainAllowEmailChangeDenialReason.Allowed;
    }
}
