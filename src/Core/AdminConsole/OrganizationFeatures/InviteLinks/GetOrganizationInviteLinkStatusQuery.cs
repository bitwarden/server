using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Core.Auth.Repositories;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

public class GetOrganizationInviteLinkStatusQuery(
    IOrganizationInviteLinkRepository organizationInviteLinkRepository,
    IOrganizationRepository organizationRepository,
    IApplicationCacheService applicationCacheService,
    ISsoConfigRepository ssoConfigRepository,
    IPolicyRepository policyRepository)
    : IGetOrganizationInviteLinkStatusQuery
{
    public async Task<CommandResult<OrganizationInviteLinkStatus>> GetStatusAsync(Guid code)
    {
        var inviteLink = await organizationInviteLinkRepository.GetByCodeAsync(code);
        if (inviteLink is null)
        {
            return new InviteLinkNotFound();
        }

        var organization = await organizationRepository.GetByIdAsync(inviteLink.OrganizationId);
        if (organization is null)
        {
            return new InviteLinkNotFound();
        }

        var ability = await applicationCacheService.GetOrganizationAbilityAsync(organization.Id);
        if (ability is null || !ability.UseInviteLinks)
        {
            return new InviteLinkNotAvailable();
        }

        var occupied = (await organizationRepository
            .GetOccupiedSeatCountByOrganizationIdAsync(organization.Id)).Total;
        var seatsAvailable = !organization.Seats.HasValue
            || occupied < organization.Seats.Value
            || (organization.MaxAutoscaleSeats.HasValue
                && organization.Seats.Value < organization.MaxAutoscaleSeats.Value);

        var sso = seatsAvailable ? await GetSsoStatusAsync(organization) : null;

        return new OrganizationInviteLinkStatus(organization.Id, organization.Name, seatsAvailable, sso);
    }

    private async Task<OrganizationInviteLinkSsoStatus?> GetSsoStatusAsync(Organization organization)
    {
        var ssoConfig = await ssoConfigRepository.GetByOrganizationIdAsync(organization.Id);
        if (ssoConfig is not { Enabled: true } || organization.Identifier is null)
        {
            return null;
        }

        var requireSsoPolicy = await policyRepository
            .GetByOrganizationIdTypeAsync(organization.Id, PolicyType.RequireSso);
        return new OrganizationInviteLinkSsoStatus(
            OrgSsoId: organization.Identifier,
            Required: requireSsoPolicy?.Enabled == true);
    }
}
