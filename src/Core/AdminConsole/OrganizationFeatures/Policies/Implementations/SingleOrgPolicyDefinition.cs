#nullable enable

using System.ComponentModel;
using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Auth.Enums;
using Bit.Core.Auth.Repositories;
using Bit.Core.Context;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.Implementations;

public class SingleOrgPolicyDefinition : IPolicyDefinition
{
    public PolicyType Type => PolicyType.SingleOrg;
    public IEnumerable<PolicyType> RequiredPolicies => Array.Empty<PolicyType>();

    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IMailService _mailService;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly ISsoConfigRepository _ssoConfigRepository;
    private readonly ICurrentContext _currentContext;

    public SingleOrgPolicyDefinition(
        IOrganizationUserRepository organizationUserRepository,
        IMailService mailService,
        IOrganizationRepository organizationRepository,
        ISsoConfigRepository ssoConfigRepository,
        ICurrentContext currentContext)
    {
        _organizationUserRepository = organizationUserRepository;
        _mailService = mailService;
        _organizationRepository = organizationRepository;
        _ssoConfigRepository = ssoConfigRepository;
        _currentContext = currentContext;
    }

    public async Task OnSaveSideEffectsAsync(Policy? currentPolicy, Policy modifiedPolicy)
    {
        if (currentPolicy is null or { Enabled: false } && modifiedPolicy is { Enabled: true })
        {
            await RemoveNonCompliantUsersAsync(modifiedPolicy.OrganizationId);
        }
    }

    private async Task RemoveNonCompliantUsersAsync(Guid organizationId)
    {
        // Remove non-compliant users
        var savingUserId = _currentContext.UserId;
        var orgUsers = await _organizationUserRepository.GetManyDetailsByOrganizationAsync(organizationId);
        var org = await _organizationRepository.GetByIdAsync(organizationId);
        if (org == null)
        {
            throw new NotFoundException("Organization not found.");
        }

        var removableOrgUsers = orgUsers.Where(ou =>
            ou.Status != OrganizationUserStatusType.Invited &&
            ou.Status != OrganizationUserStatusType.Revoked &&
            ou.Type != OrganizationUserType.Owner &&
            ou.Type != OrganizationUserType.Admin &&
            ou.UserId != savingUserId
            ).ToList();

        var userOrgs = await _organizationUserRepository.GetManyByManyUsersAsync(
                removableOrgUsers.Select(ou => ou.UserId!.Value));
        foreach (var orgUser in removableOrgUsers)
        {
            if (userOrgs.Any(ou => ou.UserId == orgUser.UserId
                        && ou.OrganizationId != org.Id
                        && ou.Status != OrganizationUserStatusType.Invited))
            {
                // TODO: OrganizationService causes a circular dependency here, we either pass it into all handlers
                // TODO: like we do with PolicyService at the moment, or we investigate and break that circle
                // await _organizationService.DeleteUserAsync(policy.OrganizationId, orgUser.Id,
                //     savingUserId);
                await _mailService.SendOrganizationUserRemovedForPolicySingleOrgEmailAsync(
                    org.DisplayName(), orgUser.Email);
            }
        }
    }

    public async Task<string?> ValidateAsync(Policy? currentPolicy, Policy modifiedPolicy)
    {
        var organizationId = modifiedPolicy.OrganizationId;

        // Do not allow this policy to be disabled if Key Connector is being used
        var ssoConfig = await _ssoConfigRepository.GetByOrganizationIdAsync(organizationId);
        if (ssoConfig?.GetData()?.MemberDecryptionType == MemberDecryptionType.KeyConnector)
        {
            return "Key Connector is enabled.";
        }

        return null;
    }
}
