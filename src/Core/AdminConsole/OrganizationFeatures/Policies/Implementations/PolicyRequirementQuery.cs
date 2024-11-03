using Bit.Core.AdminConsole.Enums;
using Bit.Core.AdminConsole.Repositories;
using Bit.Core.Exceptions;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.Implementations;

public class PolicyRequirementQuery : IPolicyRequirementQuery
{
    private readonly IPolicyRepository _policyRepository;
    private readonly IEnumerable<IPolicyRequirementDefinition<IPolicyRequirement>> _policyRequirementDefinitions;
    private readonly IOrganizationUserRepository _organizationUserRepository;
    private readonly IApplicationCacheService _applicationCacheService;

    public PolicyRequirementQuery()
    {
        // TODO: deps
    }

    // Note: PolicyType parameter can be removed once legacy uses are updated
    public async Task<T> GetAsync<T>(Guid userId, PolicyType type) where T : IPolicyRequirement
    {
        var definition = _policyRequirementDefinitions.SingleOrDefault(def =>
            def is IPolicyRequirementDefinition<T> &&
            def.Type == type);

        if (definition is null)
        {
            throw new BadRequestException("No Policy Requirement Definition found for " + typeof(T));
        }

        var userPolicyDetails =
            (await _organizationUserRepository.GetByUserIdWithPolicyDetailsAsync(userId, definition.Type)).ToList();
        var orgAbilities = await GetOrganizationAbilities(userPolicyDetails);

        var enforceablePolicies = userPolicyDetails
            .Where(p =>
                p.PolicyEnabled &&
                orgAbilities[p.OrganizationId].UsePolicies &&
                definition.FilterPredicate(p));

        // Why is cast necessary? maybe because it could be a *different* IPolicyRequirement?
        return (T)definition.Reduce(enforceablePolicies);
    }

    private async Task<IDictionary<Guid, OrganizationAbility>> GetOrganizationAbilities(IEnumerable<OrganizationUserPolicyDetails> userPolicyDetails)
    {
        // TODO: this should be in the policy repository
        var orgAbilities = await _applicationCacheService.GetOrganizationAbilitiesAsync();

        var missingOrgAbility =
            userPolicyDetails.FirstOrDefault(up => !orgAbilities.TryGetValue(up.OrganizationId, out _));
        if (missingOrgAbility is not null)
        {
            throw new BadRequestException("Invalid organization ID " + missingOrgAbility.OrganizationId);
        }

        return orgAbilities;
    }
}
