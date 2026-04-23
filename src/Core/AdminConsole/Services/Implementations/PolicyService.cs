// FIXME: Update this file to be null safe and then delete the line below
#nullable disable

using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;
using Bit.Core.Entities;
using Bit.Core.Enums;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Core.Services;
using Bit.Core.Settings;

namespace Bit.Core.AdminConsole.Services.Implementations;

public class PolicyService : IPolicyService
{
    private readonly IApplicationCacheService _applicationCacheService;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IPolicyRequirementQuery _policyRequirementQuery;
    private readonly GlobalSettings _globalSettings;

    public PolicyService(
        IApplicationCacheService applicationCacheService,
        IOrganizationUserRepository organizationUserRepository,
        IPolicyRequirementQuery policyRequirementQuery,
        GlobalSettings globalSettings)
    {
        _applicationCacheService = applicationCacheService;
        _organizationUserRepository = organizationUserRepository;
        _policyRequirementQuery = policyRequirementQuery;
        _globalSettings = globalSettings;
    }

    public async Task<MasterPasswordPolicyData> GetMasterPasswordPolicyForUserAsync(User user)
    {
        var requirement = await _policyRequirementQuery.GetAsyncVNext<MasterPasswordPolicyRequirement>(user.Id);
        return requirement.EnforcedOptions;
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

        var filteredPolicyDetails = organizationUserPolicyDetails
            .Where(o => !o.IsProvider)
            .Where(o => o.OrganizationUserStatus >= minStatus)
            .Where(o => !excludedUserTypes.Contains(o.OrganizationUserType))
            .Where(o => o.PolicyEnabled)
            .ToList();

        var orgAbilities = await GetOrganizationAbilitiesAsync(filteredPolicyDetails);

        return filteredPolicyDetails.Where(userPolicyDetails =>
        {
            if (orgAbilities.TryGetValue(userPolicyDetails.OrganizationId, out var orgAbility) && !orgAbility.UsePolicies)
            {
                return false;
            }

            return true;
        });
    }

    private async Task<IDictionary<Guid, OrganizationAbility>> GetOrganizationAbilitiesAsync(List<OrganizationUserPolicyDetails> filteredPolicyDetails)
    {
        var orgIds = filteredPolicyDetails
            .Select(o => o.OrganizationId)
            .Distinct()
            .ToList();
        var orgAbilities = await _applicationCacheService.GetOrganizationAbilitiesAsync(orgIds);
        return orgAbilities;
    }

    private OrganizationUserType[] GetUserTypesExcludedFromPolicy(PolicyType policyType)
    {
        switch (policyType)
        {
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
