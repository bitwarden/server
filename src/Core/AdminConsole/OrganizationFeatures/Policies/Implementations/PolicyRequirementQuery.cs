#nullable enable

using Bit.Core.Exceptions;
using Bit.Core.Models.Data.Organizations;
using Bit.Core.Models.Data.Organizations.OrganizationUsers;
using Bit.Core.Repositories;
using Bit.Core.Services;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.Implementations;

public class PolicyRequirementQuery(
    IEnumerable<IPolicyRequirementFactory<IPolicyRequirement>> policyRequirementFactories,
    IOrganizationUserRepository organizationUserRepository,
    IApplicationCacheService applicationCacheService
    ) : IPolicyRequirementQuery
{
    public async Task<T> GetAsync<T>(Guid userId) where T : IPolicyRequirement
    {
        var definition = (IPolicyRequirementFactory<T>?)policyRequirementFactories
            .SingleOrDefault(def => def is IPolicyRequirementFactory<T>);

        if (definition is null)
        {
            throw new BadRequestException("No Policy Requirement Factory found for " + typeof(T));
        }

        var userPolicyDetails =
            (await organizationUserRepository.GetByUserIdWithPolicyDetailsAsync(userId, definition.Type)).ToList();
        var orgAbilities = await GetOrganizationAbilitiesAsync(userPolicyDetails);

        var enforceablePolicies = userPolicyDetails
            .Where(p =>
                p.PolicyEnabled &&
                orgAbilities[p.OrganizationId].UsePolicies &&
                definition.EnforcePolicy(p));

        return definition.CreateRequirement(enforceablePolicies);
    }

    private async Task<IDictionary<Guid, OrganizationAbility>> GetOrganizationAbilitiesAsync(IEnumerable<OrganizationUserPolicyDetails> userPolicyDetails)
    {
        var orgAbilities = await applicationCacheService.GetOrganizationAbilitiesAsync();

        var missingOrgAbility =
            userPolicyDetails.FirstOrDefault(up => !orgAbilities.TryGetValue(up.OrganizationId, out _));
        if (missingOrgAbility is not null)
        {
            throw new BadRequestException("Invalid organization ID " + missingOrgAbility.OrganizationId);
        }

        return orgAbilities;
    }
}
