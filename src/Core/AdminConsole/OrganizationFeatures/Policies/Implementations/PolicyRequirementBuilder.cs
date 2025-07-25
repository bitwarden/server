using Bit.Core.AdminConsole.Models.Data.Organizations.Policies;
using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies.Implementations;

public class PolicyRequirementBuilder
{
    public static T Build<T>(IEnumerable<IPolicyRequirementFactory<IPolicyRequirement>> factories, IEnumerable<PolicyDetails> policyDetails) where T : IPolicyRequirement
    {
        var factory = factories.OfType<IPolicyRequirementFactory<T>>().SingleOrDefault();
        if (factory is null)
        {
            throw new NotImplementedException("No Requirement Factory found for " + typeof(T));
        }

        var filteredPolicies = policyDetails
            .Where(p => p.PolicyType == factory.PolicyType)
            .Where(factory.Enforce);
        var requirement = factory.Create(filteredPolicies);
        return requirement;
    }
}
