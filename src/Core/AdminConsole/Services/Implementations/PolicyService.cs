using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;

namespace Bit.Core.AdminConsole.Services.Implementations;

public class PolicyService : IPolicyService
{
    private readonly IApplicationCacheService _applicationCacheService;
    private readonly IEventService _eventService;
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IPolicyRepository _policyRepository;
    private readonly GlobalSettings _globalSettings;
    private readonly IEnumerable<IPolicyStrategy> _policyStrategies;

    public PolicyService(
        IApplicationCacheService applicationCacheService,
        IEventService eventService,
        IOrganizationRepository organizationRepository,
        IOrganizationUserRepository organizationUserRepository,
        IPolicyRepository policyRepository,
        GlobalSettings globalSettings,
        IEnumerable<IPolicyStrategy> policyStrategies)
    {
        _applicationCacheService = applicationCacheService;
        _eventService = eventService;
        _organizationRepository = organizationRepository;
        _organizationUserRepository = organizationUserRepository;
        _policyRepository = policyRepository;
        _globalSettings = globalSettings;
        _policyStrategies = policyStrategies;
    }

    public async Task SaveAsync(Policy policy, IUserService userService, IOrganizationService organizationService,
        Guid? savingUserId)
    {
        // TODO: this could use the cache
        var org = await _organizationRepository.GetByIdAsync(policy.OrganizationId);
        if (org == null)
        {
            throw new BadRequestException("Organization not found");
        }

        if (!org.UsePolicies)
        {
            throw new BadRequestException("This organization cannot use policies.");
        }

        var currentPolicyIsEnabled =
            policy.Id != default &&
            (await _policyRepository.GetByIdAsync(policy.Id))?.Enabled == true;

        var policyStrategy = _policyStrategies.Single(strategy => strategy.Type == policy.Type);
        if (!currentPolicyIsEnabled && policy.Enabled)
        {
            await policyStrategy.HandleEnable(policy, savingUserId);
        }
        else if (currentPolicyIsEnabled && !policy.Enabled)
        {
            await policyStrategy.HandleDisable(policy, savingUserId);
        }

        // Note: If the policy's Enabled state is not changing, no handler actions are called.
        // This may need to be changed in the future if a policy has additional requirements.

        var now = DateTime.UtcNow;
        if (policy.Id == default)
        {
            policy.CreationDate = now;
        }

        policy.RevisionDate = now;

        await _policyRepository.UpsertAsync(policy);
        await _eventService.LogPolicyEventAsync(policy, EventType.Policy_Updated);
    }

    public async Task<MasterPasswordPolicyData> GetMasterPasswordPolicyForUserAsync(User user)
    {
        var policies = (await _policyRepository.GetManyByUserIdAsync(user.Id))
            .Where(p => p.Type == PolicyType.MasterPassword && p.Enabled)
            .ToList();

        if (!policies.Any())
        {
            return null;
        }

        var enforcedOptions = new MasterPasswordPolicyData();

        foreach (var policy in policies)
        {
            enforcedOptions.CombineWith(policy.GetDataModel<MasterPasswordPolicyData>());
        }

        return enforcedOptions;
    }

    public async Task<ICollection<OrganizationUserPolicyDetails>> GetPoliciesApplicableToUserAsync(Guid userId, PolicyType policyType, OrganizationUserStatusType minStatus = OrganizationUserStatusType.Accepted)
    {
        var result = await QueryOrganizationUserPolicyDetailsAsync(userId, policyType, minStatus);
        return result.ToList();
    }

    public async Task<bool> AnyPoliciesApplicableToUserAsync(Guid userId, PolicyType policyType, OrganizationUserStatusType minStatus = OrganizationUserStatusType.Accepted)
    {
        var result = await QueryOrganizationUserPolicyDetailsAsync(userId, policyType, minStatus);
        return result.Any();
    }

    private async Task<IEnumerable<OrganizationUserPolicyDetails>> QueryOrganizationUserPolicyDetailsAsync(Guid userId, PolicyType policyType, OrganizationUserStatusType minStatus = OrganizationUserStatusType.Accepted)
    {
        var organizationUserPolicyDetails = await _organizationUserRepository.GetByUserIdWithPolicyDetailsAsync(userId, policyType);
        var excludedUserTypes = GetUserTypesExcludedFromPolicy(policyType);
        var orgAbilities = await _applicationCacheService.GetOrganizationAbilitiesAsync();
        return organizationUserPolicyDetails.Where(o =>
            (!orgAbilities.ContainsKey(o.OrganizationId) || orgAbilities[o.OrganizationId].UsePolicies) &&
            o.PolicyEnabled &&
            !excludedUserTypes.Contains(o.OrganizationUserType) &&
            o.OrganizationUserStatus >= minStatus &&
            !o.IsProvider);
    }

    private OrganizationUserType[] GetUserTypesExcludedFromPolicy(PolicyType policyType)
    {
        switch (policyType)
        {
            case PolicyType.MasterPassword:
                return Array.Empty<OrganizationUserType>();
            case PolicyType.RequireSso:
                // If 'EnforceSsoPolicyForAllUsers' is set to true then SSO policy applies to all user types otherwise it does not apply to Owner or Admin
                if (_globalSettings.Sso.EnforceSsoPolicyForAllUsers)
                {
                    return Array.Empty<OrganizationUserType>();
                }
                break;
        }

        return new[] { OrganizationUserType.Owner, OrganizationUserType.Admin };
    }
}
