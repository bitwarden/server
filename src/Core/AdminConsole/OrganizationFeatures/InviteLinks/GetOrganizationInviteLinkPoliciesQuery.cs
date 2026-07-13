using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Core.Repositories;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

public class GetOrganizationInviteLinkPoliciesQuery(
    IOrganizationInviteLinkRepository organizationInviteLinkRepository,
    IOrganizationRepository organizationRepository,
    IPolicyRepository policyRepository)
    : IGetOrganizationInviteLinkPoliciesQuery
{
    public async Task<CommandResult<ICollection<Policy>>> GetPoliciesAsync(Guid organizationId, Guid code)
    {
        var inviteLink = await organizationInviteLinkRepository.GetByOrganizationIdAsync(organizationId);
        if (inviteLink is null || !InviteLinkCodeValidator.CodesMatch(code.ToString(), inviteLink.Code))
        {
            return new InviteLinkNotFound();
        }

        var organization = await organizationRepository.GetByIdAsync(inviteLink.OrganizationId);
        if (organization is null or { Enabled: false })
        {
            return new InviteLinkNotFound();
        }

        if (!organization.UseInviteLinks)
        {
            return new InviteLinkNotAvailable();
        }

        if (!organization.UsePolicies)
        {
            return new InviteLinkNotFound();
        }

        var policies = await policyRepository.GetManyByOrganizationIdAsync(organization.Id);
        var enabledPolicies = policies.Where(p => p.Enabled).ToList();

        return new CommandResult<ICollection<Policy>>(enabledPolicies);
    }
}
