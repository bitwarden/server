using Bit.Core.AdminConsole.Entities;
using Bit.Core.AdminConsole.Enums;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies;

public interface IPolicyRequirement;

public interface IPolicyRequirementDefinition<T> where T : IPolicyRequirement
{
    PolicyType Type { get; }
    T Reduce(IEnumerable<Policy> policies);
    bool FilterPredicate(Policy policy);
}
