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

public record SingleOrgRequirement(bool SingleOrgRequired);

public class SingleOrgPolicyDefinition : IPolicyDefinition<SingleOrgRequirement>
{
    public PolicyType Type => PolicyType.SingleOrg;

    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IMailService _mailService;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IPolicyRepository _policyRepository;
    private readonly ISsoConfigRepository _ssoConfigRepository;
    private readonly ICurrentContext _currentContext;

    public SingleOrgPolicyDefinition(
        IOrganizationUserRepository organizationUserRepository,
        IMailService mailService,
        IOrganizationRepository organizationRepository,
        IPolicyRepository policyRepository,
        ISsoConfigRepository ssoConfigRepository,
        ICurrentContext currentContext)
    {
        _organizationUserRepository = organizationUserRepository;
        _mailService = mailService;
        _organizationRepository = organizationRepository;
        _policyRepository = policyRepository;
        _ssoConfigRepository = ssoConfigRepository;
        _currentContext = currentContext;
    }


    public Predicate<(OrganizationUser orgUser, Policy policy)> Filter => tuple =>
        tuple.orgUser is not { Type: OrganizationUserType.Owner or OrganizationUserType.Admin };

    public (Func<SingleOrgRequirement, Policy, SingleOrgRequirement> reducer, SingleOrgRequirement initialValue) Reducer() =>
        ((SingleOrgRequirement init, Policy next, SingleOrgRequirement ) => new SingleOrgRequirement(true), new SingleOrgRequirement(false));

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

        // Do not allow this policy to be disabled if a dependent policy is still enabled
        var policies = await _policyRepository.GetManyByOrganizationIdAsync(organizationId);
        if (policies.Any(p => p.Type == PolicyType.RequireSso && p.Enabled))
        {
            return "Single Sign-On Authentication policy is enabled.";
        }

        if (policies.Any(p => p.Type == PolicyType.MaximumVaultTimeout && p.Enabled))
        {
            return "Maximum Vault Timeout policy is enabled.";
        }

        if (policies.Any(p => p.Type == PolicyType.ResetPassword && p.Enabled))
        {
            return "Account Recovery policy is enabled.";
        }

        // Do not allow this policy to be disabled if Key Connector is being used
        var ssoConfig = await _ssoConfigRepository.GetByOrganizationIdAsync(organizationId);
        if (ssoConfig?.GetData()?.MemberDecryptionType == MemberDecryptionType.KeyConnector)
        {
            return "Key Connector is enabled.";
        }

        return null;
    }
}
