#nullable enable

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
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IPolicyRepository _policyRepository;
    private readonly GlobalSettings _globalSettings;
    private readonly Dictionary<PolicyType, IPolicyDefinition> _policyDefinitions = new();

    public PolicyService(
        IApplicationCacheService applicationCacheService,
        IEventService eventService,
        IOrganizationUserRepository organizationUserRepository,
        IPolicyRepository policyRepository,
        GlobalSettings globalSettings,
        IEnumerable<IPolicyDefinition> policyDefinitions)
    {
        _applicationCacheService = applicationCacheService;
        _eventService = eventService;
        _organizationUserRepository = organizationUserRepository;
        _policyRepository = policyRepository;
        _globalSettings = globalSettings;

        foreach (var policyDefinition in policyDefinitions)
        {
           _policyDefinitions.Add(policyDefinition.Type, policyDefinition);
           // TODO: throw if any policyDefinition is missing
        }
    }

    public async Task SaveAsync(Policy policy, IUserService userService, IOrganizationService organizationService,
        Guid? savingUserId)
    {
        var org = await _applicationCacheService.GetOrganizationAbilityAsync(policy.OrganizationId);
        if (org == null)
        {
            throw new BadRequestException("Organization not found");
        }

        if (!org.UsePolicies)
        {
            throw new BadRequestException("This organization cannot use policies.");
        }

        var policyDefinition = _policyDefinitions[policy.Type];
        var allSavedPolicies = await _policyRepository.GetManyByOrganizationIdAsync(org.Id);
        var currentPolicy = allSavedPolicies.SingleOrDefault(p => p.Id == policy.Id);

        // If enabling this policy - check that all policy requirements are satisfied
        if (currentPolicy is not { Enabled: true } && policy.Enabled)
        {
            foreach (var requiredPolicyType in policyDefinition.RequiredPolicies)
            {
                if (allSavedPolicies.SingleOrDefault(p => p.Type == requiredPolicyType) is not { Enabled: true })
                {
                    // TODO: would be better to reference the name instead of the enum
                    throw new BadRequestException("Policy requires PolicyType " + requiredPolicyType + " to be enabled first.");
                }
            }
        }

        // If disabling this policy - ensure it's not required by any other policy
        if (currentPolicy is { Enabled: true } && !policy.Enabled)
        {
            var dependentPolicies = _policyDefinitions.Values
                .Where(policyDef => policyDef.RequiredPolicies.Contains(policy.Type))
                .Select(policyDef => policyDef.Type)
                .Select(otherPolicyType => allSavedPolicies.SingleOrDefault(p => p.Type == otherPolicyType))
                .Where(otherPolicy => otherPolicy is { Enabled: true })
                .ToList();

            if (dependentPolicies is { Count: > 0})
            {
                throw new BadRequestException("This policy is required by " + dependentPolicies.First() + ". Try disabling that policy first." );
            }
        }

        // Run other validation
        var validationError = await policyDefinition.ValidateAsync(currentPolicy, policy);
        if (validationError != null)
        {
            throw new BadRequestException(validationError);
        }

        // Run side effects
        await policyDefinition.OnSaveSideEffectsAsync(currentPolicy, policy);

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
