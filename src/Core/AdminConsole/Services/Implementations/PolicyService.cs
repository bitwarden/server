// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
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
    private readonly IFeatureService _featureService;
    private readonly IPolicyRequirementQuery _policyRequirementQuery;

    public PolicyService(
        IApplicationCacheService applicationCacheService,
        IOrganizationUserRepository organizationUserRepository,
        IPolicyRepository policyRepository,
        GlobalSettings globalSettings,
        IFeatureService featureService,
        IPolicyRequirementQuery policyRequirementQuery)
    {
        _applicationCacheService = applicationCacheService;
        _organizationUserRepository = organizationUserRepository;
        _policyRepository = policyRepository;
        _globalSettings = globalSettings;
        _featureService = featureService;
        _policyRequirementQuery = policyRequirementQuery;
    }

    public async Task<MasterPasswordPolicyData> GetMasterPasswordPolicyForUserAsync(User user)
    {
        if (_featureService.IsEnabled(FeatureFlagKeys.PolicyRequirements))
        {
            var masterPaswordPolicy = (await _policyRequirementQuery.GetAsync<MasterPasswordPolicyRequirement>(user.Id));

            if (!masterPaswordPolicy.Enabled)
            {
                return null;
            }

            return masterPaswordPolicy.EnforcedOptions;
        }

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

        OrganizationUserType[] excludedUserTypes;
        var appliesToProviders = false;

        if (policyType == PolicyType.SingleOrg
            && _featureService.IsEnabled(FeatureFlagKeys.AutomaticConfirmUsers)
            && (await _organizationUserRepository.GetByUserIdWithPolicyDetailsAsync(userId, PolicyType.AutomaticUserConfirmation)).Any())
        {
            minStatus = OrganizationUserStatusType.Revoked;
            excludedUserTypes = [];
            appliesToProviders = true;
        }
        else
        {
            excludedUserTypes = GetUserTypesExcludedFromPolicy(policyType);
        }

        var orgAbilities = await _applicationCacheService.GetOrganizationAbilitiesAsync();

        return organizationUserPolicyDetails.Where(o =>
            (!orgAbilities.TryGetValue(o.OrganizationId, out var orgAbility) || orgAbility.UsePolicies) &&
            o.PolicyEnabled &&
            !excludedUserTypes.Contains(o.OrganizationUserType) &&
            o.OrganizationUserStatus >= minStatus &&
            (o.IsProvider && appliesToProviders || !o.IsProvider)); // the user is a provider and the policy applies to providers, or they are not a provider
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
