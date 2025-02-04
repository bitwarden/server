using Bit.Core.AdminConsole.OrganizationFeatures.Policies.PolicyRequirements;

namespace Bit.Core.AdminConsole.OrganizationFeatures.Policies;

public interface IPolicyRequirementQuery
{
    Task<T> GetAsync<T>(Guid userId) where T : IPolicyRequirement;
}
