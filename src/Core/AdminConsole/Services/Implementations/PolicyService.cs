using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;

namespace Bit.Core.AdminConsole.Services.Implementations;

public class PolicyService : IPolicyService
{
    private readonly IApplicationCacheService _applicationCacheService;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IPolicyRepository _policyRepository;
    private readonly GlobalSettings _globalSettings;

    public PolicyService(
        IApplicationCacheService applicationCacheService,
        IOrganizationUserRepository organizationUserRepository,
        IPolicyRepository policyRepository,
        GlobalSettings globalSettings)
    {
        _applicationCacheService = applicationCacheService;
        _organizationUserRepository = organizationUserRepository;
        _policyRepository = policyRepository;
        _globalSettings = globalSettings;
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
