using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks.Interfaces;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.AdminConsole.Utilities.v2.Results;
using Bit.Core.Auth.Repositories;
using Bit.Core.Repositories;

namespace Bit.Core.AdminConsole.OrganizationFeatures.InviteLinks;

public class GetOrganizationInviteLinkStatusQuery(
    IOrganizationInviteLinkRepository organizationInviteLinkRepository,
    IOrganizationRepository organizationRepository,
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
        if (organization is null or { Enabled: false })
        {
            return new InviteLinkNotFound();
        }

        if (!organization.UseInviteLinks)
        {
            return new InviteLinkNotAvailable();
        }

        var occupied = (await organizationRepository
            .GetOccupiedSeatCountByOrganizationIdAsync(organization.Id)).Total;
        var seatsAvailable = !organization.Seats.HasValue
            || occupied < organization.Seats.Value
            || !organization.MaxAutoscaleSeats.HasValue
            || organization.Seats.Value < organization.MaxAutoscaleSeats.Value;

        var sso = seatsAvailable ? await GetSsoStatusAsync(organization) : null;

        // A wrapped organization key on the link means link-confirm is enabled: a joining user is
        // link-confirmed (Confirm flow). Its absence means Accept only (admin confirms each member).
        var linkConfirmEnabled = inviteLink.EncryptedOrgKey is not null;

        return new OrganizationInviteLinkStatus(organization.Name, seatsAvailable, linkConfirmEnabled, sso);
    }

    private async Task<OrganizationInviteLinkSsoStatus?> GetSsoStatusAsync(Organization organization)
    {
        if (!organization.UseSso)
        {
            return null;
        }

        var ssoConfig = await ssoConfigRepository.GetByOrganizationIdAsync(organization.Id);
        if (ssoConfig is not { Enabled: true } || organization.Identifier is null)
        {
            return null;
        }

        var required = organization.UsePolicies
            && (await policyRepository.GetByOrganizationIdTypeAsync(organization.Id, PolicyType.RequireSso))?.Enabled == true;

        return new OrganizationInviteLinkSsoStatus(
            OrgSsoId: organization.Identifier,
            Required: required);
    }
}
